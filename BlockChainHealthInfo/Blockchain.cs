using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo
{
    public class Blockchain
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string CompressedData { get; set; }

        [Required]
        [StringLength(64)]
        public string Hash { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [Required]
        [StringLength(64)]
        public string PreviousHash { get; set; }

        [Required]
        public string EntityType { get; set; }

        [Required]
        public Guid EntityId { get; set; }

        [Required]
        public string ModifiedBy { get; set; }
        public byte[] Version { get; set; }
    }

}
