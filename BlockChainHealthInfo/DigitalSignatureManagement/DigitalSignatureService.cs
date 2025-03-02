using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public class DigitalSignatureService : IDigitalSignatureService
    {
        private readonly IKeyStorage _keyStorage;
        private readonly ILogger<DigitalSignatureService> _logger;
        private readonly KeyGenerator _keyGenerator;
        private readonly ConcurrentDictionary<Guid, KeyPoolSnapshot> _activeSnapshots = new();
        private readonly SemaphoreSlim _rotationLock = new(1, 1);

        private readonly TimeSpan _shiftInterval = TimeSpan.FromHours(2);
        // Lock object for thread safety
        private readonly object _keyPoolLock = new object();

        public DigitalSignatureService(IKeyStorage keyStorage, ILogger<DigitalSignatureService> logger)
        {
            _keyStorage = keyStorage;
            _logger = logger;
            _keyGenerator = new KeyGenerator(keyStorage);
        }

        public byte[] GetFullSignedData(byte[] data, DateTime recordDate, TimeSpan expiry)
        {
            DateTime effectiveExpiry = recordDate.Add(expiry);
            byte[] recordDateBytes = BitConverter.GetBytes(recordDate.Ticks);
            byte[] expiryBytes = BitConverter.GetBytes(effectiveExpiry.Ticks);
            return data.Concat(recordDateBytes).Concat(expiryBytes).ToArray();
        }

        public (byte[] Signature, string SnapshotVersion) SignData(byte[] data, DateTime timestamp, TimeSpan expiry)
        {
            var snapshot = CreateSnapshot();

            try
            {
                // Prepare data with expiry
                DateTime effectiveExpiry = timestamp.Add(expiry);
                byte[] expiryBytes = BitConverter.GetBytes(effectiveExpiry.Ticks);
                byte[] dataWithExpiry = data.Concat(expiryBytes).ToArray();

                _logger.LogInformation("Data with expiry (Signing): {Data}", Convert.ToBase64String(dataWithExpiry));
                _logger.LogInformation("Expiry (Signing): {Expiry}", effectiveExpiry);

                // Select key
                int keyIndex = KeySelection(snapshot.KeyPool, effectiveExpiry);
                if (keyIndex < 0 || keyIndex >= snapshot.KeyPool.Count)
                    throw new CryptographicException("Invalid key index");

                var keyEntry = snapshot.KeyPool[keyIndex];

                // Reconstruct private key with entropy
                ECParameters parameters = ParsePrivateKey(
                    Convert.FromBase64String(keyEntry.PrivateKey),
                    Convert.FromBase64String(keyEntry.Entropy)
                );

                // Sign data
                byte[] ecdsaSignature;
                using (var ecdsa = ECDsa.Create(parameters))
                {
                    ecdsaSignature = ecdsa.SignData(dataWithExpiry, HashAlgorithmName.SHA512);
                }

                // Compute HMAC
                byte[] hmacSignature;
                using (var hmac = new HMACSHA256(_keyStorage.SignKey))
                {
                    hmacSignature = hmac.ComputeHash(dataWithExpiry);
                }

                _logger.LogInformation("HMAC Key (Signing): {Key}", Convert.ToBase64String(_keyStorage.CurrentKey));
                _logger.LogInformation("Computed HMAC (Signing): {Computed}", Convert.ToBase64String(hmacSignature));

                // Package signatures
                byte[] signature = PackageSignatures(ecdsaSignature, hmacSignature, keyIndex);

                // Return signature and snapshot version
                return (signature, snapshot.Id.ToString());
            }
            finally
            {
                //DisposeSnapshot(snapshot.Id);
                //_keyStorage.SaveSnapshot(snapshot);
            }
        }

        public bool VerifySignature(byte[] dataWithExpiry, byte[] signedData, string snapshotVersion)
        {
            try
            {
                // Basic validation
                if (dataWithExpiry == null || dataWithExpiry.Length < sizeof(long))
                {
                    _logger.LogWarning("Invalid dataWithExpiry: Null or too short");
                    return false;
                }

                // Extract expiration timestamp
                long expiryTicks = BitConverter.ToInt64(dataWithExpiry, dataWithExpiry.Length - sizeof(long));
                DateTime dataExpiry = new DateTime(expiryTicks, DateTimeKind.Utc);

                if (dataExpiry < DateTime.UtcNow)
                {
                    _logger.LogWarning("Signature expired: {Expiry} < {Now}", dataExpiry, DateTime.UtcNow);
                    return false;
                }

                // Unpack signatures
                var (_, _, keyIndex) = UnpackageSignatures(signedData);

                // Get key pool snapshot by version
                if (!Guid.TryParse(snapshotVersion, out Guid snapshotId))
                {
                    _logger.LogWarning("Invalid snapshot version: {Version}", snapshotVersion);
                    return false;
                }

                // Load snapshot
                if (!_activeSnapshots.TryGetValue(snapshotId, out var snapshot))
                {
                    snapshot = _keyStorage.LoadSnapshot(snapshotId);
                    if (snapshot == null)
                    {
                        _logger.LogWarning("Snapshot not found: {Id}", snapshotId);
                        return false;
                    }
                    _activeSnapshots.TryAdd(snapshotId, snapshot);
                }

                // Recompute expected key index
                int expectedIndex = KeySelection(snapshot.KeyPool, dataExpiry);

                // Validate index matches selection algorithm
                if (keyIndex != expectedIndex)
                {
                    _logger.LogWarning("Key index mismatch: {Provided} vs {Expected}", keyIndex, expectedIndex);
                    return false;
                }

                // Validate key expiration
                var keyEntry = snapshot.KeyPool[keyIndex];
                DateTime keyExpiry = DateTime.Parse(keyEntry.ExpiryTimestamp);

                if (keyExpiry <= dataExpiry)
                {
                    _logger.LogWarning("Key expired before document: {KeyExpiry} <= {DataExpiry}", keyExpiry, dataExpiry);
                    return false;
                }

                return true; // Validation passed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification failed");
                return false;
            }
        }

        public bool VerifySignature2(byte[] dataWithExpiry, byte[] signedData, string snapshotVersion)
        {
            try
            {
                // Basic validation
                if (dataWithExpiry == null || dataWithExpiry.Length < sizeof(long))
                {
                    _logger.LogWarning("Invalid dataWithExpiry: Null or too short");
                    return false;
                }

                // Log dataWithExpiry for debugging
                _logger.LogInformation("Data with expiry (Verification): {Data}", Convert.ToBase64String(dataWithExpiry));

                // Extract expiration timestamp
                long expiryTicks = BitConverter.ToInt64(dataWithExpiry, dataWithExpiry.Length - sizeof(long));
                DateTime dataExpiry = new DateTime(expiryTicks, DateTimeKind.Utc);
                _logger.LogInformation("Expiry (Verification): {Expiry}", dataExpiry);

                if (dataExpiry < DateTime.UtcNow)
                {
                    _logger.LogWarning("Signature expired: {Expiry} < {Now}", dataExpiry, DateTime.UtcNow);
                    return false;
                }

                // Unpack signatures
                var (ecdsaSig, hmacSig, keyIndex) = UnpackageSignatures(signedData);

                // Get key pool snapshot by version
                if (!Guid.TryParse(snapshotVersion, out Guid snapshotId))
                {
                    _logger.LogWarning("Invalid snapshot version: {Version}", snapshotVersion);
                    return false;
                }

                // Try to get snapshot from in-memory cache
                if (!_activeSnapshots.TryGetValue(snapshotId, out var snapshot))
                {
                    // If not found in memory, load from persistent storage
                    snapshot = _keyStorage.LoadSnapshot(snapshotId);
                    if (snapshot == null)
                    {
                        _logger.LogWarning("Snapshot not found: {Id}", snapshotId);
                        return false;
                    }

                    // Add to in-memory cache for future use
                    _activeSnapshots.TryAdd(snapshotId, snapshot);
                }

                // Validate key index range
                if (keyIndex < 0 || keyIndex >= snapshot.KeyPool.Count)
                {
                    _logger.LogWarning("Invalid key index: {Index} (Pool size: {Size})", keyIndex, snapshot.KeyPool.Count);
                    return false;
                }

                // Get key entry
                var keyEntry = snapshot.KeyPool[keyIndex];
                DateTime keyExpiry = DateTime.Parse(keyEntry.ExpiryTimestamp);
                _logger.LogInformation("Key expiry: {Expiry}", keyExpiry);

                // Key must expire AFTER document
                if (keyExpiry <= dataExpiry)
                {
                    _logger.LogWarning("Key expired before document: {KeyExpiry} <= {DataExpiry}", keyExpiry, dataExpiry);
                    return false;
                }

                // Log HMAC key for debugging
                //_logger.LogInformation("HMAC Key (Verification): {Key}", Convert.ToBase64String(_keyStorage.SignKey));

                // Cryptographic validation
                using var hmac = new HMACSHA256(_keyStorage.SignKey);
                byte[] computedHmac = hmac.ComputeHash(dataWithExpiry);
                _logger.LogInformation("Computed HMAC (Verification): {Computed}", Convert.ToBase64String(computedHmac));
                _logger.LogInformation("Stored HMAC: {Stored}", Convert.ToBase64String(hmacSig));

                if (!computedHmac.SequenceEqual(hmacSig))
                {
                    _logger.LogWarning("HMAC validation failed");
                    return false;
                }

                // Reconstruct public key
                var publicParams = ParsePublicKey(
                    Convert.FromBase64String(keyEntry.PublicKey),
                    Convert.FromBase64String(keyEntry.Entropy)
                );

                // Verify ECDSA signature
                using var ecdsa = ECDsa.Create(publicParams);
                bool isSignatureValid = ecdsa.VerifyData(dataWithExpiry, ecdsaSig, HashAlgorithmName.SHA512);
                _logger.LogInformation("ECDSA Signature Validation Result: {Result}", isSignatureValid);

                return isSignatureValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification failed");
                return false;
            }
        }



        private int KeySelection(List<KeyGenerator.KeyEntry> keyPool, DateTime documentTimestamp)
        {
            const double KeyPoolPercentage = 0.2;
            int poolSegment = (int)(keyPool.Count * KeyPoolPercentage);

            // Stable hash calculation
            byte[] timestampBytes = BitConverter.GetBytes(documentTimestamp.Ticks);
            if (BitConverter.IsLittleEndian) Array.Reverse(timestampBytes);

            using var sha256 = SHA256.Create();
            int index = Math.Abs(BitConverter.ToInt32(sha256.ComputeHash(timestampBytes), 0)) % poolSegment;

            // Find first valid key in segment
            for (int i = index; i < poolSegment; i++)
            {
                if (DateTime.Parse(keyPool[i].ExpiryTimestamp) > documentTimestamp)
                    return i;
            }

            throw new CryptographicException("No valid key found");
        }


        public void UpdateKeyPool(List<KeyGenerator.KeyEntry> reIndexedPool)
        {
            _keyStorage.SaveKeyPool(reIndexedPool);
            _logger.LogInformation($"Key pool updated with {reIndexedPool.Count} entries.");
        }

        // Key rotation implementation
        public async Task ShiftSignatureKeys()
        {
            _logger.LogInformation("Rotating signature keys...");

            lock (_keyPoolLock) // Acquire lock for thread safety
            {
                // Load the current key pool from storage
                var currentPool = _keyStorage.LoadKeyPool();
                if (currentPool.Count == 0) return;

                // Rotate keys by moving the first 20% to the end
                int rotateCount = Math.Max(1, currentPool.Count / 20);
                var rotatedPool = new List<KeyGenerator.KeyEntry>(
                    currentPool.Skip(rotateCount).Concat(currentPool.Take(rotateCount))
                );

                // Update expiration for rotated keys
                var newExpiry = DateTime.UtcNow.Add(_shiftInterval).AddHours(2);
                foreach (var key in rotatedPool)
                {
                    key.ExpiryTimestamp = newExpiry.ToString("o");
                }

                // Save the rotated key pool
                UpdateKeyPool(rotatedPool);
                _logger.LogInformation($"Rotated {rotateCount} keys. New expiration: {newExpiry}");
            }
        }

        public async Task RegenerateKeys()
        {
            _logger.LogInformation("Regenerating cryptographic material...");

            lock (_keyPoolLock) 
            {
                // Generate a new master key
                _keyStorage.UpdateMasterKey();

                // Generate a fresh key pool using the new master key
                var newPool = _keyGenerator.GetKeyPool(_keyStorage.CurrentKey);

                // Validate the new key pool
                if (newPool.Count != KeyGenerator.KeyPoolSize)
                {
                    throw new CryptographicException("Invalid key pool size after regeneration");
                }

                // Save the new key pool
                UpdateKeyPool(newPool);
                _logger.LogInformation("Full key regeneration completed");
            }
        }

        private KeyPoolSnapshot CreateSnapshot()
        {
            var snapshot = new KeyPoolSnapshot
            {
                Id = Guid.NewGuid(),
                KeyPool = _keyStorage.LoadKeyPool(),
                SignKey = _keyStorage.SignKey // Capture the current SignKey
            };

            _keyStorage.SaveSnapshot(snapshot);
            _activeSnapshots.TryAdd(snapshot.Id, snapshot);
            return snapshot;
        }

        // Helper methods
        private ECParameters ParsePrivateKey(byte[] privateKeyBytes, byte[] entropy)
        {
            using (var tempEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521))
            {
                tempEcdsa.ImportECPrivateKey(privateKeyBytes, out _);
                return tempEcdsa.ExportParameters(true);
            }
        }


        private ECParameters ParsePublicKey(byte[] publicKeyBytes, byte[] entropy)
        {
            using (var tempEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521))
            {
                tempEcdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
                ECParameters parameters = tempEcdsa.ExportParameters(false);

                return parameters;
            }
        }

        private void ValidateTicks(long ticks)
        {
            try
            {
                // Use DateTime's built-in validation
                var _ = new DateTime(ticks, DateTimeKind.Utc);
            }
            catch
            {
                throw new ArgumentOutOfRangeException(nameof(ticks),
                    $"Invalid timestamp value: {ticks}. Valid range: {DateTime.MinValue.Ticks} to {DateTime.MaxValue.Ticks}");
            }
        }

        private byte[] PackageSignatures(byte[] ecdsaSig, byte[] hmacSig, int keyIndex)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(ecdsaSig.Length);
                bw.Write(ecdsaSig);
                bw.Write(hmacSig.Length);
                bw.Write(hmacSig);
                bw.Write(keyIndex);
                return ms.ToArray();
            }
        }


        private void DisposeSnapshot(Guid snapshotId)
        {
            _activeSnapshots.TryRemove(snapshotId, out _);
        }


        private (byte[] ecdsaSig, byte[] hmacSig, int keyIndex) UnpackageSignatures(byte[] signedData)
        {
            using (var ms = new MemoryStream(signedData))
            using (var br = new BinaryReader(ms))
            {
                return (
                    br.ReadBytes(br.ReadInt32()),
                    br.ReadBytes(br.ReadInt32()),
                    br.ReadInt32()
                );
            }
        }

        public static byte[] GetFullSignedData(byte[] data, TimeSpan expiry)
        {
            DateTime effectiveExpiry = DateTime.UtcNow.Add(expiry);
            byte[] expiryBytes = BitConverter.GetBytes(effectiveExpiry.Ticks);
            return data.Concat(expiryBytes).ToArray();
        }
    }

    public class SignatureExpiredException : Exception
    {
        public SignatureExpiredException(string message) : base(message) { }
    }

    public class KeyPoolSnapshot
    {
        public Guid Id { get; set; }
        public List<KeyGenerator.KeyEntry> KeyPool { get; set; }
        public byte[] SignKey { get; set; }
    }


}
