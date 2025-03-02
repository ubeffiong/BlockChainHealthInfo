using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public interface IDigitalSignatureService
    {

        (byte[] Signature, string SnapshotVersion) SignData(byte[] data, DateTime timestamp, TimeSpan expiry);
        void UpdateKeyPool(List<KeyGenerator.KeyEntry> reIndexedPool);
        Task ShiftSignatureKeys();
        Task RegenerateKeys();
        bool VerifySignature(byte[] dataWithExpiry, byte[] signedData, string snapshotVersion);

        byte[]  GetFullSignedData(byte[] data, DateTime timestamp, TimeSpan expiry);


    }
}
