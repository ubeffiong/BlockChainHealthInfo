using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public class SignedEntity
    {
        public int Id { get; set; }
        public string Data { get; set; }
        public byte[] Signature { get; set; }
    }
}
