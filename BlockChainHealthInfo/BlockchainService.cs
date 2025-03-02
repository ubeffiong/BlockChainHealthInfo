using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo
{

    public class BlockchainService
    {
        private readonly AppDbContext _context;
        private readonly string _secretKey;
        private readonly object _blockLock = new object();

        public BlockchainService(AppDbContext context, string secretKey)
        {
            _context = context;
            _secretKey = secretKey;
        }

        public void AddBlock<T>(Blockchain block) where T : class
        {
            lock (_blockLock)
            {
                using var transaction = _context.Database.BeginTransaction();
                try
                {
                    var lastBlock = GetLastBlock<T>(block.EntityId);
                    if (lastBlock == null)
                    {
                        var genesisBlock = CreateGenesisBlockForEntity(block);
                        _context.Blockchains.Add(genesisBlock);
                        _context.SaveChanges();
                        lastBlock = genesisBlock;
                    }

                    block.PreviousHash = lastBlock.Hash;
                    block.Timestamp = DateTime.UtcNow;
                    block.Hash = CalculateHash(block);

                    _context.Blockchains.Add(block);
                    _context.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception("Error adding block", ex);
                }
            }
        }

        private Blockchain CreateGenesisBlockForEntity(Blockchain block)
        {
            var genesisBlock = new Blockchain
            {
                CompressedData = block.CompressedData,
                EntityId = block.EntityId,
                EntityType = block.EntityType, // Use existing value
                Timestamp = DateTime.UtcNow,
                Version = block.Version,
                ModifiedBy = "System",
                PreviousHash = "GENESIS",
                //Signature = block.Signature
            };

            genesisBlock.Hash = CalculateHash(genesisBlock);
            return genesisBlock;
        }


        private string CalculateHash(Blockchain block)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var rawData = $"{block.CompressedData}-{block.Timestamp}-{block.PreviousHash}-{block.EntityType}-{block.ModifiedBy}"; // -{block.Signature} - - - if block.Id is add the hash will not be consistent after saving, bcus When calling CalculateHash in the AddBlock method, the Id is probably still zero, but after saving the block to the database, it gets a new Id. Timestamp Formatting can also affect it
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData)));
        }

        public (bool IsValid, string Message) ValidateChain<T>(Guid entityId) where T : class
        {
            var blocks = _context.Blockchains
                .Where(b => b.EntityType == typeof(T).Name && b.EntityId == entityId)
                .OrderBy(b => b.Id)
                .ToList();

            if (blocks.Count == 0)
            {
                return (false, "No blocks found for the specified entity.");
            }

            for (int i = 1; i < blocks.Count; i++)
            {
                var current = blocks[i];
                var previous = blocks[i - 1];

                // Check link consistency: current block's PreviousHash should match the hash of the previous block.
                if (current.PreviousHash != previous.Hash)
                {
                    return (false, $"Link consistency failed at block Id {current.Id}. Expected previous hash '{previous.Hash}', but got '{current.PreviousHash}'.");
                }

                // Check data integrity: current block's stored hash should match the calculated hash from its data.
                var calculatedHash = CalculateHash(current);
                if (current.Hash != calculatedHash)
                {
                    return (false, $"Data integrity failed at block Id {current.Id}. Expected hash '{calculatedHash}', but got '{current.Hash}'.");
                }
            }

            return (true, "Chain is valid.");
        }



        private Blockchain GetLastBlock<T>(Guid entityId) where T : class
        {
            return _context.Blockchains
                .Where(b => b.EntityType == typeof(T).Name && b.EntityId == entityId)
                .OrderByDescending(b => b.Id)
                .FirstOrDefault();
        }

    }
}
