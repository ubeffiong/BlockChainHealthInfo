//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Security.Cryptography;
//using System.Security;
//using System.Text;
//using System.Threading.Tasks;
//using Tpm2Lib;
//using Microsoft.Azure.KeyVault;
//using Microsoft.EntityFrameworkCore;
//using System.Reflection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.EntityFrameworkCore.Diagnostics;
//using System.Data.Common;
//using System.Collections.Concurrent;
//using Microsoft.Data.SqlClient;
//using System.Security.Cryptography.X509Certificates;
//using Microsoft.Extensions.DependencyInjection;
//using System.Runtime.InteropServices;
//using Azure.Identity;
//using Hl7.Fhir.Model.CdsHooks;
//using System.ComponentModel.DataAnnotations;
//using Microsoft.Azure.KeyVault.Models;
//using System.Diagnostics;

//namespace BlockChainHealthInfo
//{
//    public class CentralizedKeyManager : IDisposable
//    {
//        #region Centralized Key Manager

//        private readonly bool _isCloud;
//        private readonly CngProvider _cngProvider;
//        private readonly KeyVaultClient _keyVaultClient;
//        private readonly Tpm2Device _tpmDevice;
//        private readonly MfaChallengeService _mfaService;

//        public CentralizedKeyManager(bool isCloud, MfaChallengeService mfaService, string cloudConnectionString = null)
//        {
//            _isCloud = isCloud;
//            _cngProvider = CngProvider.MicrosoftSoftwareKeyStorageProvider;
//            _mfaService = mfaService;

//            if (_isCloud)
//            {
//                var credential = new DefaultAzureCredential();
//                _keyVaultClient = new KeyVaultClient(async (authority, resource, scope) =>
//                {
//                    var token = await credential.GetTokenAsync(
//                        new Azure.Core.TokenRequestContext(new[] { "https://vault.azure.net/.default" }));
//                    return token.Token;
//                });
//            }
//            else
//            {
//                // Instantiate and connect to the TPM device.
//                _tpmDevice = new TbsDevice();
//                _tpmDevice.Connect();
//            }
//        }

//        /// <summary>
//        /// Creates a key pair using CNG and stores the private key securely (in TPM or Key Vault).
//        /// </summary>
//        public async Task<CngKey> CreateKeyPairAsync(string keyName, Dictionary<string, string> environmentalValues)
//        {
//            var keyParams = new CngKeyCreationParameters
//            {
//                Provider = _cngProvider,
//                KeyCreationOptions = CngKeyCreationOptions.OverwriteExistingKey
//            };

//            // Add environmental values as key properties.
//            foreach (var kvp in environmentalValues)
//            {
//                keyParams.Parameters.Add(new CngProperty(kvp.Key, Encoding.UTF8.GetBytes(kvp.Value), CngPropertyOptions.None));
//            }

//            var key = CngKey.Create(CngAlgorithm.ECDsaP521, keyName, keyParams);

//            if (_isCloud)
//            {
//                // Create key in Azure Key Vault.
//                KeyBundle keyBundle = await _keyVaultClient.CreateKeyAsync(
//                    "https://your-vault.vault.azure.net/",
//                    keyName,
//                    new KeyAttributes());
//                // Persist the key reference (e.g. in your database)
//                // _dbContext.KeyVaultReferences.Add(new KeyVaultReference { KeyName = keyName, KeyId = keyBundle.KeyIdentifier.Identifier });
//                // await _dbContext.SaveChangesAsync();
//            }
//            else
//            {
//                // Store key on TPM.
//                TpmHandle keyHandle = _tpmDevice.CreatePersistedKey(TpmRh.Owner, TpmAlgId.Ecdsa, keyName);
//                _tpmDevice.LoadKey(keyHandle);
//            }

//            return key;
//        }


//        /// <summary>
//        /// Signs data using a key retrieved via environmental values and MFA validation.
//        /// </summary>
//        public byte[] SignData(byte[] data, string keyName, Dictionary<string, string> environmentalValues)
//        {
//            // Validate MFA.
//            if (!environmentalValues.ContainsKey("MfaToken") ||
//                !_mfaService.ValidateHardwareToken(environmentalValues["MfaToken"]))
//            {
//                throw new SecurityException("MFA validation failed");
//            }

//            CngKey key = RetrieveKey(keyName, environmentalValues);

//            // Validate temporal properties.
//            if (!new TemporalKeyPolicy().IsKeyValid(key))
//            {
//                throw new SecurityException("Expired key");
//            }

//            using (var ecdsa = new ECDsaCng(key))
//            {
//                ecdsa.HashAlgorithm = CngAlgorithm.Sha512;
//                return ecdsa.SignData(data);
//            }
//        }

//        /// <summary>
//        /// Verifies the signature of data.
//        /// </summary>
//        public bool VerifyData(byte[] data, byte[] signature, string keyName, Dictionary<string, string> environmentalValues)
//        {
//            // Validate MFA.
//            if (!environmentalValues.ContainsKey("MfaToken") ||
//                !_mfaService.ValidateHardwareToken(environmentalValues["MfaToken"]))
//            {
//                throw new SecurityException("MFA validation failed");
//            }

//            CngKey key = RetrieveKey(keyName, environmentalValues);
//            if (!new TemporalKeyPolicy().IsKeyValid(key))
//            {
//                throw new SecurityException("Expired key");
//            }

//            using (var ecdsa = new ECDsaCng(key))
//            {
//                ecdsa.HashAlgorithm = CngAlgorithm.Sha512;
//                return ecdsa.VerifyData(data, signature);
//            }
//        }


//        /// <summary>
//        /// Retrieves the key from either the cloud or TPM after validating environmental values.
//        /// </summary>
//        private CngKey RetrieveKey(string keyName, Dictionary<string, string> environmentalValues)
//        {
//            // Optional: Generate/validate an MFA challenge here if desired.
//            if (!environmentalValues.ContainsKey("MfaToken") ||
//                !_mfaService.ValidateHardwareToken(environmentalValues["MfaToken"]))
//            {
//                throw new SecurityException("MFA required for key access");
//            }

//            if (_isCloud)
//            {
//                KeyBundle keyBundle = _keyVaultClient.GetKeyAsync("https://your-vault.vault.azure.net/", keyName).Result;
//                // Assuming keyBundle.Key is convertible to a key blob. (Adjust as needed.)
//                byte[] keyBlob = Convert.FromBase64String(keyBundle.Key.ToString());
//                return CngKey.Import(keyBlob, CngKeyBlobFormat.EccPrivateBlob);
//            }
//            else
//            {
//                TpmHandle keyHandle = _tpmDevice.GetKeyHandle(keyName);
//                return ReconstructKeyFromEnvironment(keyHandle, environmentalValues);
//            }
//        }

//        /// <summary>
//        /// Uses environmental properties stored on the TPM to validate and reconstruct the key.
//        /// </summary>
//        private CngKey ReconstructKeyFromEnvironment(TpmHandle keyHandle, Dictionary<string, string> environmentalValues)
//        {
//            var storedProps = _tpmDevice.GetKeyProperties(keyHandle);
//            foreach (var kvp in environmentalValues)
//            {
//                if (!storedProps.TryGetValue(kvp.Key, out var storedVal) || storedVal != kvp.Value)
//                {
//                    throw new SecurityException("Environmental validation failed");
//                }
//            }
//            return _tpmDevice.GetPublicKey(keyHandle);
//        }


//        /// <summary>
//        /// Example method to revoke a key (e.g. when an anomaly is detected).
//        /// </summary>
//        public void RevokeKey(string keyIdentifier, string reason)
//        {
//            // Implement key revocation logic here (e.g. mark key as revoked in your database).
//            Debug.WriteLine($"Key {keyIdentifier} revoked. Reason: {reason}");
//        }

//        public void Dispose()
//        {
//            _tpmDevice?.Dispose();
//        }

//    }


//    #endregion

//    #region Environmental & Temporal Services


//    public class EnvironmentalKeyService
//    {
//        public Dictionary<string, string> GetCurrentEnvironmentalValues(string userId)
//        {
//            return new Dictionary<string, string>
//            {
//                { "Day", DateTime.UtcNow.Day.ToString() },
//                { "Month", DateTime.UtcNow.Month.ToString() },
//                { "UserID", userId },
//                { "AppVersion", Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0" }
//            };
//        }

//        public string GenerateRotationKeyHash(DbContext dbContext, string secret)
//        {
//            // For demonstration, we use dummy properties. Adjust based on your DB schema.
//            var dbProperties = new
//            {
//                LastModified = dbContext.Database.GetDbConnection().ServerVersion,
//                UserCount = dbContext.Set<User>().Count()
//            };

//            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret)))
//            {
//                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dbProperties)));
//                return Convert.ToBase64String(hash);
//            }
//        }
//    }


//    public class TemporalKeyPolicy
//    {
//        public bool IsKeyValid(CngKey key)
//        {
//            try
//            {
//                var validFromProp = key.GetProperty("ValidFrom", CngPropertyOptions.None);
//                var validToProp = key.GetProperty("ValidTo", CngPropertyOptions.None);

//                long validFrom = BitConverter.ToInt64(validFromProp.GetValue(), 0);
//                long validTo = BitConverter.ToInt64(validToProp.GetValue(), 0);

//                var validFromDate = DateTimeOffset.FromUnixTimeSeconds(validFrom);
//                var validToDate = DateTimeOffset.FromUnixTimeSeconds(validTo);

//                return DateTimeOffset.UtcNow >= validFromDate && DateTimeOffset.UtcNow <= validToDate;
//            }
//            catch
//            {
//                return false;
//            }
//        }
//    }

//    #endregion

//    #region Key Rotation Service


//    public class KeyRotationService : BackgroundService
//    {
//        private readonly DbContext _dbContext;
//        private readonly CentralizedKeyManager _keyManager;

//        public KeyRotationService(DbContext dbContext, CentralizedKeyManager keyManager)
//        {
//            _dbContext = dbContext;
//            _keyManager = keyManager;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                var now = DateTimeOffset.UtcNow;

//                // For demonstration, assume you have a table of keys with temporal properties.
//                var expiringKeys = _dbContext.Set<KeyVaultReference>()
//                    .Where(k => EF.Property<DateTimeOffset>(k, "ValidTo") < now.AddDays(30))
//                    .ToList();

//                foreach (var key in expiringKeys)
//                {
//                    var newKey = await _keyManager.CreateKeyPairAsync(
//                        $"Key_{Guid.NewGuid()}",
//                        new Dictionary<string, string>
//                        {
//                            { "ValidFrom", now.ToUnixTimeSeconds().ToString() },
//                            { "ValidTo", now.AddYears(1).ToUnixTimeSeconds().ToString() }
//                        });

//                    // Archive old key mapping (implementation depends on your database design)
//                    //_dbContext.KeyArchive.Add(new KeyArchive
//                    //{
//                    //    OldKeyName = key.Name,
//                    //    NewKeyName = newKey.Name,
//                    //    ValidFrom = key.ValidFrom,
//                    //    ValidTo = key.ValidTo
//                    //});
//                }

//                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
//            }
//        }
//    }

//    #endregion

//    #region Database Command Interceptors



//    public class SignatureEnforcementInterceptor : DbCommandInterceptor
//    {
//        private readonly EnvironmentalKeyService _environmentalService;
//        private readonly CentralizedKeyManager _keyManager;

//        public SignatureEnforcementInterceptor(EnvironmentalKeyService environmentalService, CentralizedKeyManager keyManager)
//        {
//            _environmentalService = environmentalService;
//            _keyManager = keyManager;
//        }

//        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
//            DbCommand command,
//            CommandEventData eventData,
//            InterceptionResult<DbDataReader> result,
//            CancellationToken cancellationToken = default)
//        {
//            var user = eventData.Context.GetCurrentUser(); // Extension method on DbContext.
//            var envValues = _environmentalService.GetCurrentEnvironmentalValues(user.Id);

//            if (!command.Parameters.Contains("@signature"))
//                throw new SecurityException("Unsigned database operation");

//            string signatureParam = command.Parameters["@signature"].Value.ToString();
//            byte[] computedHash = ComputeCommandHash(command.CommandText);

//            // Using "CurrentKey" as a placeholder key name.
//            if (!_keyManager.VerifyData(computedHash, Convert.FromBase64String(signatureParam), "CurrentKey", envValues))
//                throw new SecurityException("Invalid operation signature");

//            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
//        }

//        private byte[] ComputeCommandHash(string commandText)
//        {
//            using (var sha = SHA512.Create())
//            {
//                return sha.ComputeHash(Encoding.UTF8.GetBytes(commandText));
//            }
//        }
//    }





//    public class MfaChallengeService
//    {
//        private readonly YubicoClient _yubicoClient = new YubicoClient();
//        private readonly TotpService _totpService = new TotpService();

//        public bool ValidateHardwareToken(string yubiKeyOtp)
//        {
//            return _yubicoClient.ValidateOtp(yubiKeyOtp);
//        }

//        public bool ValidateSoftwareToken(string secret, string code)
//        {
//            return _totpService.Validate(secret, code, TimeSpan.FromSeconds(30));
//        }

//        public string GenerateMfaChallenge(string userId)
//        {
//            var challenge = new
//            {
//                Nonce = Guid.NewGuid().ToString(),
//                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
//                UserId = userId
//            };
//            return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(challenge)));
//        }
//    }







//    public class BehavioralAnomalyDetector
//    {
//        private readonly ConcurrentDictionary<string, UserBehaviorProfile> _profiles = new();
//        private readonly CentralizedKeyManager _keyManager;
//        private readonly IAlertService _alertService;

//        public BehavioralAnomalyDetector(CentralizedKeyManager keyManager, IAlertService alertService)
//        {
//            _keyManager = keyManager;
//            _alertService = alertService;
//        }

//        public void AnalyzeOperation(User user, DatabaseOperation operation)
//        {
//            var profile = _profiles.GetOrAdd(user.Id, id => new UserBehaviorProfile());
//            double riskScore = CalculateRiskScore(profile, operation);

//            if (riskScore > 0.8)
//            {
//                _keyManager.RevokeKey(user.PublicKey, $"Anomaly detected: {operation.Type}");
//                _alertService.TriggerIncident(IncidentResponse.FullLockdown);
//            }

//            profile.Update(operation);
//        }

//        private double CalculateRiskScore(UserBehaviorProfile profile, DatabaseOperation operation)
//        {
//            double score = 0;
//            if (operation.Type == OperationType.BulkDelete)
//                score += 0.4 * Math.Min(operation.RecordCount / 1000.0, 1.0);
//            if (operation.Time.Hour < 8 || operation.Time.Hour > 18)
//                score += 0.3;
//            if (profile.AvgOperationsPerHour < 5 && operation.RecordCount > 50)
//                score += 0.6;
//            return score;
//        }
//    }

//    public record DatabaseOperation(OperationType Type, int RecordCount, DateTimeOffset Time);

//    #endregion

//    #region Code Signing & Secure DLL Loader







//    public class AnomalyDetectionInterceptor : DbCommandInterceptor
//    {
//        private readonly BehavioralAnomalyDetector _anomalyDetector;

//        public AnomalyDetectionInterceptor(BehavioralAnomalyDetector anomalyDetector)
//        {
//            _anomalyDetector = anomalyDetector;
//        }

//        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
//            DbCommand command,
//            CommandEventData eventData,
//            InterceptionResult<int> result,
//            CancellationToken cancellationToken = default)
//        {
//            var user = eventData.Context.GetCurrentUser();
//            OperationType opType = DetectOperationType(command.CommandText);
//            int recordCount = GetAffectedRecordCount(command);
//            var operation = new DatabaseOperation(opType, recordCount, DateTimeOffset.UtcNow);

//            _anomalyDetector.AnalyzeOperation(user, operation);

//            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
//        }

//        private OperationType DetectOperationType(string commandText)
//        {
//            if (commandText.IndexOf("DELETE", StringComparison.OrdinalIgnoreCase) >= 0)
//                return OperationType.BulkDelete;
//            return OperationType.Unknown;
//        }

//        private int GetAffectedRecordCount(DbCommand command)
//        {
//            // Placeholder logic: parse command text or use parameters to determine record count.
//            return 1;
//        }
//    }

//    #endregion

//    #region MFA & Behavioral Anomaly Detection



//    public static class CodeSigningValidator
//    {
//        public static void ValidateAssemblySignatures()
//        {
//            var assemblies = new[]
//            {
//                typeof(DbContext).Assembly,
//                typeof(SqlConnection).Assembly,
//                typeof(CentralizedKeyManager).Assembly
//            };

//            foreach (var assembly in assemblies)
//            {
//                X509Certificate cert = X509Certificate.CreateFromSignedFile(assembly.Location);
//                var verifier = new AuthenticodeSignatureVerifier(cert);

//                if (!verifier.VerifySignature())
//                    throw new SecurityException($"Tampered assembly: {assembly.FullName}");

//                ValidatePublisher(cert);
//            }
//        }

//        private static void ValidatePublisher(X509Certificate2 cert)
//        {
//            const string ExpectedThumbprint = "A389..."; // Replace with your organization's certificate thumbprint.
//            if (!cert.Thumbprint.Equals(ExpectedThumbprint, StringComparison.OrdinalIgnoreCase))
//                throw new SecurityException("Untrusted publisher");
//        }
//    }

//    public class SecureDriverLoader
//    {
//        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
//        private static extern bool SetDllDirectory(string lpPathName);

//        public static void Initialize()
//        {
//            const string securePath = @"\\secure-network-path\trusted-binaries";
//            if (!SetDllDirectory(securePath))
//            {
//                throw new SecurityException("Failed to set secure DLL directory");
//            }
//            CodeSigningValidator.ValidateAssemblySignatures();
//        }
//    }

//    #endregion

//    #region Audit Logging




//    //public class TemporalKeyPolicy
//    //{
//    //    public bool IsKeyValid(CngKey key)
//    //    {
//    //        var validFrom = DateTimeOffset.FromUnixTimeSeconds(
//    //            BitConverter.ToInt64(key.GetProperty("ValidFrom").GetValue()));
//    //        var validTo = DateTimeOffset.FromUnixTimeSeconds(
//    //            BitConverter.ToInt64(key.GetProperty("ValidTo").GetValue()));

//    //        return DateTimeOffset.UtcNow.IsBetween(validFrom, validTo);
//    //    }
//    //}

//    public sealed class SignedAuditLogger
//    {
//        private readonly CentralizedKeyManager _keyManager;
//        private readonly WriteOnce<AuditEntry> _auditStore;

//        public SignedAuditLogger(CentralizedKeyManager keyManager)
//        {
//            _keyManager = keyManager;
//            _auditStore = new WriteOnce<AuditEntry>(@"\\secure-path\audit.writonce", FileMode.Create);
//        }

//        public void LogOperation(DatabaseOperation operation, string userId)
//        {
//            var envValues = new EnvironmentalKeyService().GetCurrentEnvironmentalValues(userId);
//            var logEntry = new AuditEntry(operation.Type.ToString(), operation.RecordCount, DateTime.UtcNow, envValues);
//            var signature = _keyManager.SignData(logEntry.GetHash(), "AUDIT_KEY", envValues);
//            _auditStore.Write(logEntry with { Signature = signature });
//        }
//    }


//    public record AuditEntry(string OperationType, int RecordCount, DateTime Timestamp, Dictionary<string, string> EnvironmentalValues)
//    {
//        public byte[] Signature { get; init; }

//        public byte[] GetHash()
//        {
//            using (var sha = SHA512.Create())
//            {
//                string data = $"{OperationType}|{RecordCount}|{Timestamp:o}|{JsonConvert.SerializeObject(EnvironmentalValues)}";
//                return sha.ComputeHash(Encoding.UTF8.GetBytes(data));
//            }
//        }
//    }


//    public class WriteOnce<T>
//    {
//        private readonly string _filePath;
//        private readonly FileMode _fileMode;

//        public WriteOnce(string filePath, FileMode fileMode)
//        {
//            _filePath = filePath;
//            _fileMode = fileMode;
//        }

//        public void Write(T entry)
//        {
//            // For demonstration, serialize the entry to a file.
//            File.WriteAllText(_filePath, JsonConvert.SerializeObject(entry));
//        }
//    }

//    #endregion

//    #region Supporting Classes & Enums



//    public class KeyVaultReference
//    {
//        [Key]
//        public int Id { get; set; }
//        public string KeyName { get; set; }
//        public string KeyId { get; set; }
//        public DateTime Created { get; set; } = DateTime.UtcNow;
//    }

//    public class TpmKeyHandle
//    {
//        public string KeyName { get; set; }
//        public TpmHandle Handle { get; set; }
//        public Dictionary<string, string> EnvironmentalValues { get; set; }
//    }

//    public enum OperationType
//    {
//        Unknown,
//        BulkDelete
//    }

//    public class User
//    {
//        public string Id { get; set; }
//        public string PublicKey { get; set; }
//    }

//    public class UserBehaviorProfile
//    {
//        public double AvgOperationsPerHour { get; set; } = 0;
//        public void Update(DatabaseOperation operation)
//        {
//            // Update behavior profile based on the operation.
//            // (Implementation depends on your analytics logic.)
//        }
//    }

//    public interface IAlertService
//    {
//        void TriggerIncident(IncidentResponse response);
//    }

//    public enum IncidentResponse
//    {
//        FullLockdown,
//        AlertOnly
//    }

//    public static class DbContextExtensions
//    {
//        /// <summary>
//        /// Stub extension to simulate retrieval of the current user from a DbContext.
//        /// </summary>
//        public static User GetCurrentUser(this DbContext context)
//        {
//            return new User { Id = "user123", PublicKey = "AUDIT_KEY" };
//        }
//    }

//    #endregion

//    #region Stub External Services

//    // These classes are stubs. Replace with your actual implementations.
//    public class YubicoClient
//    {
//        public bool ValidateOtp(string otp)
//        {
//            // Validate YubiKey OTP.
//            return true;
//        }
//    }

//    public class TotpService
//    {
//        public bool Validate(string secret, string code, TimeSpan timeStep)
//        {
//            // Validate TOTP code.
//            return true;
//        }
//    }

//    public class AuthenticodeSignatureVerifier
//    {
//        private readonly X509Certificate _certificate;

//        public AuthenticodeSignatureVerifier(X509Certificate certificate)
//        {
//            _certificate = certificate;
//        }

//        public bool VerifySignature()
//        {
//            // Implement signature verification logic.
//            return true;
//        }
//    }

//    #endregion



//}
