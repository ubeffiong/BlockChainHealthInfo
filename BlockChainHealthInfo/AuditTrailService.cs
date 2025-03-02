using AutoMapper;
using BlockChainHealthInfo.DigitalSignatureManagement;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo
{

    public interface IAuditableEntity
    {
        Guid Id { get; set; }

        // Signed Entity
        string GenerateSignature();
        byte[] SignedDataBlob { get; set; }
        DateTime SignatureExpiry { get; set; }
        string Signature { get; set; }
        public string SnapshotVersion { get; set; }
    }

    public class AuditRecord
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public string ModifiedBy { get; set; }
        public string Changes { get; set; }
        public string EntityType { get; set; }
        public string SerializedSnapshot { get; set; }
    }

    public class AuditTrailService
    {
        private readonly BlockchainService _blockchain;
        private readonly AppDbContext _context;

        private readonly IDigitalSignatureService _signatureService;
        private readonly AuditLogger _auditLogger;


        public AuditTrailService(BlockchainService blockchain, AppDbContext context, IDigitalSignatureService signatureService,
        AuditLogger auditLogger)
        {
            _blockchain = blockchain;
            _context = context;

            _signatureService = signatureService;
            _auditLogger = auditLogger;
        }

        public void LogChanges(IAuditableEntity entity, List<AuditEntry> changes, string modifiedBy)
        {
            // 1. Reload the updated entity from the database.
            //    This ensures we have the latest state including all child and nested records.
            var updatedEntity = _context.Find(entity.GetType(), entity.Id) as IAuditableEntity;
            if (updatedEntity == null)
            {
                throw new Exception("Updated entity not found in the database.");
            }

            // 2. Serialize and compress the snapshot.
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            };

            var serialized = JsonConvert.SerializeObject(updatedEntity, settings);
            var compressed = CompressData(serialized);

            // 3. Sign the data
            var dataBytes = Encoding.UTF8.GetBytes(serialized);
            var signature = _signatureService.SignData(dataBytes, DateTime.UtcNow, TimeSpan.FromHours(1));

            // Create a blockchain block for this snapshot.
            var block = new Blockchain
            {
                CompressedData = compressed,
                EntityId = entity.Id,
                ModifiedBy = modifiedBy,
                EntityType = entity.GetType().Name,
                Version = BitConverter.GetBytes(DateTime.UtcNow.Ticks)
            };

            /// 5. Add the block to the blockchain.
            // Use reflection to call the generic AddBlock method with the correct type.
            var method = _blockchain.GetType().GetMethod("AddBlock");
            var generic = method.MakeGenericMethod(entity.GetType());
            generic.Invoke(_blockchain, new object[] { block });

            // 6. Log the audit event.
            _auditLogger.LogEvent($"Changes logged for {entity.GetType().Name} with ID {entity.Id}");
        }


        public List<AuditRecord> GetEntityHistory(Guid entityId)
        {
            return _context.Blockchains
                .Where(b => b.EntityId == entityId)
                .OrderBy(b => b.Timestamp)
                .AsEnumerable() // Switch to client-side evaluation
                .Select(b => new AuditRecord
                {
                    Timestamp = b.Timestamp,
                    Action = "Updated",
                    ModifiedBy = b.ModifiedBy,
                    EntityType = b.EntityType,
                    SerializedSnapshot = DecompressData(b.CompressedData),
                    Changes = GetChangesFromBlock(b)
                })
                .ToList();
        }

        private string GetChangesFromBlock(Blockchain block)
        {
            try
            {
                var entityType = Type.GetType($"BlockChainHealthInfo.{block.EntityType}");
                if (entityType == null)
                {
                    return "Entity type not found";
                }

                // Get the generic Set<T> method via reflection
                var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes);
                var genericSetMethod = setMethod.MakeGenericMethod(entityType);
                var query = (IQueryable)genericSetMethod.Invoke(_context, null);

                // Dynamically include related data
                query = IncludeRelatedData(query, entityType);

                // Use strongly-typed query to filter by Id
                var parameter = Expression.Parameter(entityType, "e");
                var idProperty = entityType.GetProperty("Id");
                var idValue = Expression.Constant(block.EntityId);
                var propertyAccess = Expression.Property(parameter, idProperty);
                var equalsExpression = Expression.Equal(propertyAccess, idValue);
                var lambda = Expression.Lambda(equalsExpression, parameter);

                var whereMethod = typeof(Queryable).GetMethods()
                    .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
                    .MakeGenericMethod(entityType);

                query = (IQueryable)whereMethod.Invoke(null, new object[] { query, lambda });

                var current = query.Cast<object>().FirstOrDefault();

                if (current == null)
                {
                    return $"Entity {block.EntityId} no longer exists";
                }

                var snapshot = JsonConvert.DeserializeObject(
                    DecompressData(block.CompressedData),
                    entityType);

                return CompareObjects(snapshot, current);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetChangesFromBlock: {ex.Message}");
                return "Change detection failed";
            }
        }

        private IQueryable IncludeRelatedData(IQueryable query, Type entityType)
        {
            var navigationProperties = entityType.GetProperties()
                .Where(p => p.PropertyType.IsClass && p.PropertyType != typeof(string))
                .ToList();

            foreach (var navProp in navigationProperties)
            {
                // Get the Include method via reflection
                var includeMethod = typeof(EntityFrameworkQueryableExtensions)
                    .GetMethods()
                    .First(m =>
                        m.Name == nameof(EntityFrameworkQueryableExtensions.Include) &&
                        m.GetParameters().Length == 2)
                    .MakeGenericMethod(entityType, navProp.PropertyType);

                // Build lambda expression: e => e.NavigationProperty
                var parameter = Expression.Parameter(entityType, "e");
                var propertyAccess = Expression.Property(parameter, navProp);
                var lambda = Expression.Lambda(propertyAccess, parameter);

                query = (IQueryable)includeMethod.Invoke(null, new object[] { query, lambda });
            }

            return query;
        }

        private const int MaxRecursionDepth = 10;

        private string CompareObjects(object oldObj, object newObj, string parentPath = "", int depth = 0)
        {
            if (depth > MaxRecursionDepth)
                return $"[{parentPath}: Recursion depth limit reached]";

            // Both null: no difference.
            if (oldObj == null && newObj == null)
                return "";
            if (oldObj == null)
                return $"{parentPath}: Added {newObj.GetType().Name}";
            if (newObj == null)
                return $"{parentPath}: Removed {oldObj.GetType().Name}";

            // If both are simple types, compare directly.
            if (IsSimpleType(oldObj.GetType()) && IsSimpleType(newObj.GetType()))
            {
                if (!oldObj.Equals(newObj))
                    return $"{parentPath}: {FormatValue(oldObj)} → {FormatValue(newObj)}";
                return "";
            }

            // If both are collections (but not strings), compare as lists.
            if (oldObj is IEnumerable oldEnum && newObj is IEnumerable newEnum && !(oldObj is string))
            {
                var oldList = oldEnum.Cast<object>().ToList();
                var newList = newEnum.Cast<object>().ToList();
                var changes = new List<string>();
                if (oldList.Count != newList.Count)
                {
                    changes.Add($"{parentPath}.Count: {oldList.Count} → {newList.Count}");
                }
                int count = Math.Max(oldList.Count, newList.Count);
                for (int i = 0; i < count; i++)
                {
                    string elementPath = $"{parentPath}[{i}]";
                    object oldElement = i < oldList.Count ? oldList[i] : null;
                    object newElement = i < newList.Count ? newList[i] : null;
                    var diff = CompareObjects(oldElement, newElement, elementPath, depth + 1);
                    if (!string.IsNullOrWhiteSpace(diff))
                        changes.Add(diff);
                }
                return string.Join("\n", changes);
            }

            // Otherwise, for complex objects, compare each public instance property.
            var changesList = new List<string>();
            var properties = oldObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                // Optionally, ignore properties like "Version" or "Timestamp" (adjust as needed)
                if (prop.Name == "Version" || prop.Name == "Timestamp")
                    continue;

                string currentPath = string.IsNullOrEmpty(parentPath) ? prop.Name : $"{parentPath}.{prop.Name}";
                object oldVal, newVal;
                try { oldVal = prop.GetValue(oldObj); } catch { oldVal = null; }
                try { newVal = prop.GetValue(newObj); } catch { newVal = null; }

                var diff = CompareObjects(oldVal, newVal, currentPath, depth + 1);
                if (!string.IsNullOrWhiteSpace(diff))
                    changesList.Add(diff);
            }
            return string.Join("\n", changesList);
        }

        private bool IsSimpleType(Type type)
        {
            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(DateTime) ||
                   type == typeof(decimal) ||
                   type == typeof(Guid);
        }

        private string FormatValue(object value)
        {
            if (value == null) return "[null]";
            if (value is string str) return $"'{str}'";
            if (value is DateTime dt) return dt.ToString("yyyy-MM-dd");
            return value.ToString();
        }

        private string CompressData(string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            {
                gzip.Write(bytes, 0, bytes.Length);
            }
            return Convert.ToBase64String(output.ToArray());
        }

        private static string DecompressData(string compressed)
        {
            var bytes = Convert.FromBase64String(compressed);
            using var input = new MemoryStream(bytes);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                gzip.CopyTo(output);
            }
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }


    public class AuditEntry
    {
        public string FieldName { get; }
        public object OldValue { get; }
        public object NewValue { get; }

        public AuditEntry(string fieldName, object oldValue, object newValue)
        {
            FieldName = fieldName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

}
