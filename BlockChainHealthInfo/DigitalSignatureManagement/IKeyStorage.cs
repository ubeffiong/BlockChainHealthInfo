using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BlockChainHealthInfo.DigitalSignatureManagement.KeyGenerator;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public interface IKeyStorage
    {
        byte[] CurrentKey { get; }

        Guid GetKeyPoolVersion();

        string GenerateSignature();

        byte[] SignKey { get; set; }
        void SaveKey(KeyVersioned key);
        //byte[] LoadKey();
        Task RemoveOldKeys();

        List<KeyEntry> LoadKeyPool();
        void SaveKeyPool(List<KeyEntry> keyPool); // Add the SaveKeyPool method
        Task UpdateMasterKey();  // Add the UpdateMasterKey method

        List<KeyGenerator.KeyEntry> GetKeyPool(byte[] masterKey);

        void SaveSnapshot(KeyPoolSnapshot snapshot);
        KeyPoolSnapshot LoadSnapshot(Guid snapshotId);


    }
}
