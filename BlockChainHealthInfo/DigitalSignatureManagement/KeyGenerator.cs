using Ionic.Zip;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public class KeyGenerator
    {
        private readonly IKeyStorage _keyStorage;

        public const int KeyPoolSize = 50000;

        // File paths used by the key generator.
        private static readonly string ZipFilePath = "secure_keys.zip";
        private static readonly string KeyFilePath = "secure_keys.json";
        private static readonly string BackupFilePath = "secure_keys_backup.json";
        private static readonly string ExtractedKeyPath = "extracted_keys.json";
        private static int _versionCounter = 1;
        private static readonly string _secretKey;

        private byte[] _appKey;


        static KeyGenerator()
        {
            _secretKey = Environment.GetEnvironmentVariable("smart-contract");
        }

        public KeyGenerator(IKeyStorage keyStorage)
        {
            _keyStorage = keyStorage;
        }

        

        /// <summary>
        /// Retrieves the key pool as a list of key entries.
        /// This method checks if the secure ZIP file exists (or falls back to the backup),
        /// extracts the secure_keys.json into an object using Ionic.Zip (with the environment secret),
        /// decrypts the contained key data with the provided masterKey, and then uses the decrypted entropy
        /// (combined with the master key and index) to generate a pool of ECDSA signing keys.
        /// </summary>
        public List<KeyEntry> GetKeyPool(byte[] masterKey)
        {
            // Check if the ZIP file exists; if not, attempt using the backup.
            if (!File.Exists(ZipFilePath))
            {
                Console.WriteLine("Secure keys ZIP not found, attempting to use backup...");
                if (File.Exists(BackupFilePath))
                    RecreateZipFromBackup();
                Console.WriteLine("Back Secure keys ZIP not found, attempting to create new keys...");

                // Create a new KeyVersioned object
                var keyVersioned = new KeyVersioned(masterKey, DateTime.UtcNow, _versionCounter++.ToString());
                _keyStorage.SaveKey(keyVersioned);
            }

            // Extract secure_keys.json from the ZIP (cached if possible)
            var secureData = ExtractSecureKeys();
            int keyCount = secureData.Count;

            // Preallocate the key pool list
            var keyEntriesList = new List<KeyEntry>(KeyPoolSize);

            // Parallelize key generation
            Parallel.For(0, KeyPoolSize, i =>
            {
                var entry = secureData[i % keyCount];

                // Decrypt the key
                byte[] nonce = Convert.FromBase64String(entry.Nonce);
                byte[] encryptedData = Convert.FromBase64String(entry.Data);

                // Generate entropy using HKDF
                byte[] combinedEntropy = CombineEntropy(encryptedData, masterKey, i);

                // Generate ECDSA key pair
                var (privateKey, publicKey, entropy) = GenerateECDSAKey(combinedEntropy);

                // Add the key entry to the list
                lock (keyEntriesList) // Ensure thread-safe access to the list
                {
                    keyEntriesList.Add(new KeyEntry
                    {
                        Index = i,
                        PrivateKey = Convert.ToBase64String(privateKey),
                        PublicKey = Convert.ToBase64String(publicKey),
                        Entropy = Convert.ToBase64String(entropy), // Store the entropy
                        ExpiryTimestamp = DateTime.UtcNow.AddMonths(6).ToString("o")
                    });
                }

                // Clear sensitive data
                CryptographicOperations.ZeroMemory(combinedEntropy);
            });

            // Compress and protect the key entries with Gzip
            _appKey = CompressAndProtectKeyEntries(keyEntriesList, _secretKey);

            // Save _appKey to IKeyStorage
            _keyStorage.SignKey = _appKey; // Set the SignKey property

            // Save _appKey to IKeyStorage
            _keyStorage.SaveKeyPool(keyEntriesList);

            Console.WriteLine("Application keys are ready to be used...");

            // Return the key pool
            return keyEntriesList;
        }

        public static byte[] CompressAndProtectKeyEntries(List<KeyEntry> keyEntries, string password)
        {
            // Serialize the key entries to JSON
            string json = JsonSerializer.Serialize(keyEntries);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            // Compress and encrypt the JSON using Gzip with a password
            using (var outputStream = new MemoryStream())
            {
                using (var zip = new Ionic.Zip.ZipFile())
                {
                    zip.Password = password; // Use _secretKey as the password
                    zip.AddEntry("key_entries.json", jsonBytes); // Add JSON data to the ZIP
                    zip.Save(outputStream); // Save the ZIP to a memory stream
                }

                return outputStream.ToArray(); // Return the compressed and encrypted data
            }
        }

        /// <summary>
        /// Extracts the secure_keys.json file from the ZIP archive into a dictionary.
        /// This method reads the entry into memory (instead of writing it to disk) and returns the deserialized object.
        /// </summary>
        private static List<SecureKeyEntry> ExtractSecureKeys()
        {
            if (string.IsNullOrEmpty(_secretKey))
                throw new InvalidOperationException("Environment secret key is missing.");

            string sourceZip = File.Exists(ZipFilePath) ? ZipFilePath : BackupFilePath;
            Console.WriteLine($"Extracting secure keys from {sourceZip}...");

            using (ZipFile zip = ZipFile.Read(sourceZip))
            {
                zip.Password = _secretKey;
                foreach (ZipEntry entry in zip)
                {
                    if (entry.FileName.Equals("secure_keys.json", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var ms = new MemoryStream())
                        {
                            entry.Extract(ms);
                            ms.Position = 0;
                            using (var sr = new StreamReader(ms))
                            {
                                string jsonContent = sr.ReadToEnd();
                                // Deserialize into a list of SecureKeyEntry objects
                                var secureData = JsonSerializer.Deserialize<List<SecureKeyEntry>>(jsonContent);

                                if (secureData == null || secureData.Count == 0)
                                    throw new InvalidDataException("No valid entries found in secure_keys.json.");

                                return secureData;
                            }
                        }
                    }
                }
            }
            throw new FileNotFoundException("secure_keys.json not found in the ZIP archive.");
        }

        /// <summary>
        /// Recreates the secure ZIP file from the backup file.
        /// The backup is compressed with the same secret key as used originally.
        /// </summary>
        private static void RecreateZipFromBackup()
        {
            string envSecret = Environment.GetEnvironmentVariable("smart-contract")
                               ?? throw new Exception("Environment secret key missing.");
            Console.WriteLine("Recreating ZIP from backup...");
            using (var zip = new ZipFile())
            {
                zip.Password = envSecret;
                zip.AddFile(BackupFilePath, "");
                zip.Save(ZipFilePath);
            }
        }

        /// <summary>
        /// Generates an ECDSA key pair using the nistP521 curve.
        /// The provided entropy is XOR‑mixed into the exported private key bytes.
        /// </summary>
        public static (byte[] PrivateKey, byte[] PublicKey, byte[] Entropy) GenerateECDSAKey(byte[] entropy)
        {
            using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521))
            {
                ECParameters parameters = ecdsa.ExportParameters(true);
                byte[] originalD = parameters.D;

                for (int i = 0; i < originalD.Length; i++)
                {
                    parameters.D[i] ^= entropy[i % entropy.Length];
                }

                using (var modifiedEcdsa = ECDsa.Create(parameters))
                {
                    byte[] privateKey = modifiedEcdsa.ExportECPrivateKey();
                    byte[] publicKey = modifiedEcdsa.ExportSubjectPublicKeyInfo();
                    return (privateKey, publicKey, entropy);
                }
            }
        }

        /// <summary>
        /// Combines the decrypted keys with the master key and an index value
        /// to produce unique entropy for each key in the pool.
        /// </summary>
        private static byte[] CombineEntropy(byte[] decryptedKey, byte[] masterKey, int index)
        {
            // Generate a unique salt using RNG
            byte[] salt = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);

            byte[] timestampBytes = BitConverter.GetBytes(DateTime.UtcNow.Ticks);

            // Use HKDF to derive entropy with multiple inputs
            byte[] entropy = HKDF.DeriveKey(
                hashAlgorithmName: HashAlgorithmName.SHA512,
                ikm: decryptedKey,             // Input key material (decrypted key)
                outputLength: 32,              // Output length (32 bytes)
                salt: CombineBytes(masterKey, salt), // Salt = masterKey + RNG salt
                info: CombineBytes(BitConverter.GetBytes(index), timestampBytes) // Index + timestamp
            );

            return entropy;
        }

        // Helper method to concatenate byte arrays
        private static byte[] CombineBytes(params byte[][] arrays)
        {
            byte[] combined = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, combined, offset, array.Length);
                offset += array.Length;
            }
            return combined;
        }

        /// <summary>
        /// Represents an entry in the key pool.
        /// </summary>
        public class KeyEntry
        {
            public int Index { get; set; }
            public string PrivateKey { get; set; }
            public string PublicKey { get; set; }
            public string ExpiryTimestamp { get; set; }
            //public byte[] Entropy { get;  set; }
            public string Entropy { get; set; }
            public byte[] EntropyBytes { get; set; }
        }

        public class SecureKeyEntry
        {
            public string Version { get; set; }
            public string Timestamp { get; set; }
            public string Nonce { get; set; }
            public string Data { get; set; }
        }
    }

}
