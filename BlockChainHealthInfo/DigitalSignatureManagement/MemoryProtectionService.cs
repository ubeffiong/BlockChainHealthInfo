using Konscious.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public static class MemoryProtectionService
    {
        public static byte[] DeriveKey(byte[] password, byte[] salt, int keySizeInBytes = 32)
        {
            using var argon2 = new Argon2id(password)
            {
                Salt = salt,
                DegreeOfParallelism = 4, // Adjust based on your system's capabilities
                MemorySize = 65536, // 64 MB
                Iterations = 3 // Adjust based on your security requirements
            };

            return argon2.GetBytes(keySizeInBytes);
        }

        public static byte[] Encrypt(byte[] data, byte[] key, byte[] nonce)
        {
            if (nonce.Length != 12)
                throw new ArgumentException("Nonce must be 12 bytes long for AES-GCM.");

            using var aesGcm = new AesGcm(key);

            byte[] ciphertext = new byte[data.Length];
            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes for the tag

            aesGcm.Encrypt(nonce, data, ciphertext, tag);

            CryptographicOperations.ZeroMemory(data);

            return ciphertext.Concat(tag).ToArray();
        }

        public static byte[] Decrypt(byte[] encryptedData, byte[] key, byte[] nonce)
        {
            if (nonce.Length != 12)
                throw new ArgumentException("Nonce must be 12 bytes long for AES-GCM.");

            byte[] ciphertext = encryptedData.Take(encryptedData.Length - 16).ToArray();
            byte[] tag = encryptedData.Skip(encryptedData.Length - 16).ToArray();

            using var aesGcm = new AesGcm(key);

            byte[] plaintext = new byte[ciphertext.Length];

            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

            // Clear sensitive data
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);

            return plaintext;
        }
    }

}
