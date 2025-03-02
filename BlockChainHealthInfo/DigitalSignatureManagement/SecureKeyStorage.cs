using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static BlockChainHealthInfo.DigitalSignatureManagement.KeyGenerator;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public class SecureKeyStorage : IKeyStorage
    {
        private static readonly string PassphraseFile = "passphrase.txt";
        private static readonly string PlainKeyFile = "plainKey.json";
        //private static readonly string KeyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secure_keys.zip");
        private static readonly string BackupFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "secure_keys_backup.zip");
        private static readonly string ZipFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secure_keys.zip");
        private readonly ILogger<DigitalSignatureService> _logger;

        private readonly KeyGenerator _keyGenerator;

        private byte[] _masterKey;
        private readonly List<KeyVersioned> _keyVersions = new();

        private List<byte[]> _plainKeys;

        public byte[] CurrentKey => _masterKey;

        private readonly string _secretKey;

        private byte[] _appKey;

        public byte[] SignKey
        {
            get => _appKey; 
            set => _appKey = value; 
        }


        public SecureKeyStorage(string secretKey)
        {
            _secretKey = secretKey;
            _masterKey = GenerateMasterKey();

            GeneratePlainKeys();

            _keyGenerator = new KeyGenerator(this);

            _keyGenerator.GetKeyPool(_masterKey);

        }

        private  byte[] GenerateMasterKey()
        {
            if (!File.Exists(PassphraseFile))
                throw new FileNotFoundException("Passphrase file not found.");

            // Read the passphrase
            string passphrase = File.ReadAllText(PassphraseFile).Trim();

            // Rearrange the passphrase
            passphrase = RearrangePassphrase(passphrase);

            // Get the environment secret key
            string envSecret = _secretKey;

            // Include timestamp in salt
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            byte[] salt = Encoding.UTF8.GetBytes(envSecret + timestamp);
            byte[] passBytes = Encoding.UTF8.GetBytes(passphrase);

            // Use Argon2 to derive a strong master key
            byte[] key = MemoryProtectionService.DeriveKey(passBytes, salt, keySizeInBytes: 32);
            
            return key;
        }

        public async Task UpdateMasterKey()
        {
            _masterKey = GenerateMasterKey(); // Update the instance's _masterKey field
            Console.WriteLine("Master key updated successfully.");
        }

        public static string RearrangePassphrase(string passphrase)
        {
            // Split the passphrase into individual words
            string[] words = passphrase.Split(' ');

            // Shuffle the order of the words
            Random rng = new Random();
            words = words.OrderBy(x => rng.Next()).ToArray();

            // Rearrange the characters within each word
            for (int i = 0; i < words.Length; i++)
            {
                char[] charArray = words[i].ToCharArray();
                charArray = charArray.OrderBy(x => rng.Next()).ToArray();
                words[i] = new string(charArray);
            }

            // Rejoin the words into the final passphrase
            return string.Join(" ", words);
        }



        private void GeneratePlainKeys()
        {
            _plainKeys = new List<byte[]>(50000);
            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(_secretKey);
            try
            {
                for (int i = 0; i < 50000; i++)
                {
                    // Use HKDF with index as context to derive each key
                    byte[] indexBytes = BitConverter.GetBytes(i);
                    byte[] key = HKDF.DeriveKey(
                        HashAlgorithmName.SHA256,
                        secretKeyBytes, 
                        32,             
                        indexBytes      
                    );

                    _plainKeys.Add(key);
                }
            }
            finally
            {
                // Clear sensitive data
                CryptographicOperations.ZeroMemory(secretKeyBytes);
            }

        }



        public void SaveKey(KeyVersioned key)
        {
            // Encrypt each plain key
            List<(byte[] nonce, byte[] encryptedData)> encryptedKeys = new();
            foreach (byte[] plainKey in _plainKeys)
            {
                byte[] keyBytes = Encoding.UTF8.GetBytes(Convert.ToBase64String(plainKey));

                // Generate a nonce for encryption
                byte[] nonce = new byte[12];
                RandomNumberGenerator.Fill(nonce);

                // Encrypt keys using ChaCha20-Poly1305
                byte[] encryptedData = MemoryProtectionService.Encrypt(keyBytes, _masterKey, nonce);
                encryptedKeys.Add((nonce, encryptedData));
            }

            // Save the encrypted keys to the key file
            var secureData = encryptedKeys.Select(t => new
            {
                Version = key.Version,
                Timestamp = key.Timestamp,
                Nonce = Convert.ToBase64String(t.nonce),
                Data = Convert.ToBase64String(t.encryptedData)
            }).ToList();

            string secureJson = JsonSerializer.Serialize(secureData);

            // Compress and Protect secure_keys.json using ZIP
            CompressSecureKeys(secureJson);

            // Copy the compressed ZIP file to the other locations
            //File.Copy(ZipFilePath, KeyFilePath, true);
            File.Copy(ZipFilePath, BackupFilePath, true);
        }

        // Implement the GetKeyPool method
        public List<KeyGenerator.KeyEntry> GetKeyPool(byte[] masterKey)
        {
            return _keyGenerator.GetKeyPool(masterKey);
        }

        private void CompressSecureKeys(string secureData)
        {

            string envSecret = Environment.GetEnvironmentVariable("smart-contract") ?? throw new Exception("Environment secret key missing.");

            using var zip = new Ionic.Zip.ZipFile();
            zip.Password = envSecret;
            zip.AddEntry("secure_keys.json", secureData);
            zip.Save(ZipFilePath);
        }


        public List<KeyEntry> LoadKeyPool()
        {
            string password = Environment.GetEnvironmentVariable("smart-contract") ?? throw new Exception("Environment secret key missing.");

            // Load the compressed key entries from storage
            byte[] compressedKeyEntries = File.ReadAllBytes("app_key.zip");

            // Decompress and decrypt the key entries
            using (var inputStream = new MemoryStream(compressedKeyEntries))
            using (var zip = Ionic.Zip.ZipFile.Read(inputStream))
            {
                var entry = zip["key_entries.json"]; // Get the entry by name
                if (entry == null)
                    throw new FileNotFoundException("key_entries.json not found in the ZIP archive.");

                // Set the password for decryption
                entry.Password = password;

                // Extract the entry to a memory stream
                using (var outputStream = new MemoryStream())
                {
                    entry.Extract(outputStream);
                    outputStream.Position = 0;

                    // Deserialize the JSON
                    using (var reader = new StreamReader(outputStream))
                    {
                        string json = reader.ReadToEnd();
                        var keyEntries = JsonSerializer.Deserialize<List<KeyEntry>>(json);

                        // Validate and convert Entropy from Base64 string to byte array
                        foreach (var keyEntry in keyEntries)
                        {
                            if (string.IsNullOrEmpty(keyEntry.Entropy))
                            {
                                throw new InvalidDataException("Entropy is missing or invalid in key entry.");
                            }

                            try
                            {
                                keyEntry.EntropyBytes = Convert.FromBase64String(keyEntry.Entropy);
                            }
                            catch (FormatException ex)
                            {
                                throw new InvalidDataException("Entropy is not a valid Base64 string.", ex);
                            }

                            // Validate entropy length
                            if (keyEntry.EntropyBytes == null || keyEntry.EntropyBytes.Length == 0)
                            {
                                throw new InvalidDataException("Entropy is empty or invalid.");
                            }
                        }

                        return keyEntries;
                    }
                }
            }
        }
        public async Task RemoveOldKeys()
        {
            try
            {
                _logger.LogInformation("Removing old keys...");

                // Define the cutoff time (e.g., 24 hours ago)
                var cutoff = DateTime.UtcNow - TimeSpan.FromHours(24);

                // Remove keys older than the cutoff
                _keyVersions.RemoveAll(k => k.Timestamp < cutoff);

                // Log the number of keys removed
                _logger.LogInformation($"Removed {_keyVersions.Count(k => k.Timestamp < cutoff)} old keys.");

                // Save the updated key pool
                var currentKeyPool = LoadKeyPool();
                SaveKeyPool(currentKeyPool);

                _logger.LogInformation("Old keys removed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove old keys");
                throw;
            }
        }




        public void SaveKeyPool(List<KeyEntry> keyEntries)
        {
            // Compress and protect the key entries with Gzip
            byte[] compressedKeyEntries = KeyGenerator.CompressAndProtectKeyEntries(keyEntries, _secretKey);

            // Save the compressed key entries to storage
            File.WriteAllBytes("app_key.zip", compressedKeyEntries);
        }

        Guid IKeyStorage.GetKeyPoolVersion()
        {
            throw new NotImplementedException();
        }

        string IKeyStorage.GenerateSignature()
        {
            throw new NotImplementedException();
        }

        public void SaveSnapshot(KeyPoolSnapshot snapshot)
        {
            string snapshotJson = JsonSerializer.Serialize(snapshot);
            string snapshotFileName = $"snapshot_{snapshot.Id}.json";
            File.WriteAllText(snapshotFileName, snapshotJson);
        }

        public KeyPoolSnapshot LoadSnapshot(Guid snapshotId)
        {
            string snapshotFileName = $"snapshot_{snapshotId}.json";
            if (!File.Exists(snapshotFileName))
                return null;

            string snapshotJson = File.ReadAllText(snapshotFileName);
            return JsonSerializer.Deserialize<KeyPoolSnapshot>(snapshotJson);
        }
    }

}
