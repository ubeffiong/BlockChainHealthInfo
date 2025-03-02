using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public class AuditLogger
    {
        private static readonly string AuditFilePath = "audit.log";
        private static readonly object FileLock = new();
        private MerkleTree _merkleTree = new();

        public void LogEvent(string message)
        {
            lock (FileLock)
            {
                string timestamp = DateTime.UtcNow.ToString("o");
                string logEntry = $"{timestamp} | {message}";
                _merkleTree.Add(logEntry);
                string merkleRoot = _merkleTree.RootHash;
                File.AppendAllText(AuditFilePath, $"{logEntry} | {merkleRoot}{Environment.NewLine}");
            }
        }

        public bool ValidateAuditLog()
        {
            var logEntries = File.ReadAllLines(AuditFilePath);
            return _merkleTree.Validate(logEntries);
        }
    }

    public class MerkleTree
    {
        private List<string> _leafHashes = new();
        private List<List<string>> _treeLevels = new();

        public string RootHash => _treeLevels.Count > 0 ? _treeLevels.Last()[0] : string.Empty;

        public void Add(string data)
        {
            string hash = ComputeHash(data);
            _leafHashes.Add(hash);
            RebuildTree();
        }

        public bool Validate(IEnumerable<string> entries)
        {
            string computedRoot = ComputeRootHash(entries);
            return computedRoot == RootHash;
        }

        private void RebuildTree()
        {
            _treeLevels = new List<List<string>> { _leafHashes };
            while (_treeLevels.Last().Count > 1)
            {
                var currentLevel = _treeLevels.Last();
                var nextLevel = new List<string>();
                for (int i = 0; i < currentLevel.Count; i += 2)
                {
                    string left = currentLevel[i];
                    string right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : left;
                    string parentHash = ComputeHash(left + right);
                    nextLevel.Add(parentHash);
                }
                _treeLevels.Add(nextLevel);
            }
        }

        private string ComputeRootHash(IEnumerable<string> entries)
        {
            if (!entries.Any())
                return string.Empty;

            var leafHashes = entries.Select(ComputeHash).ToList();
            var levels = new List<List<string>> { leafHashes };
            while (levels.Last().Count > 1)
            {
                var currentLevel = levels.Last();
                var nextLevel = new List<string>();
                for (int i = 0; i < currentLevel.Count; i += 2)
                {
                    string left = currentLevel[i];
                    string right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : left;
                    string parentHash = ComputeHash(left + right);
                    nextLevel.Add(parentHash);
                }
                levels.Add(nextLevel);
            }
            return levels.Last().FirstOrDefault() ?? string.Empty;
        }

        private string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
