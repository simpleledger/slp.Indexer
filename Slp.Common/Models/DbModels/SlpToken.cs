using Slp.Common.Models.Enums;
using Slp.Common.Utility;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Slp.Common.Models.DbModels
{
    //public enum TokenType { Type1 = 1 }
    /// <summary>
    /// Token is defined with genesis transaction ( token structure here is not strictly folled by protocol since genesis transaction basicall produces SlpToken and SlpMint entity at the same time
    /// </summary>
    public class SlpToken
    {
        [Key]
        //[MaxLength(SD.HashHexSize)]
        //public string Hex { get; set; }
        [MaxLength(SD.HashSize)]
        public byte[] Hash { get; set; }

        [ForeignKey(nameof(Block))]
        public int? BlockHeight { get; set; }
        public SlpBlock Block { get; set; }

        public SlpVersionType VersionType { get; set; }
        [MaxLength(SD.MessageSize)]
        public string Name { get; set; }
        [MaxLength(SD.MessageSize)]
        public string Symbol { get; set; }
        [MaxLength(SD.MessageSize)]
        public string DocumentUri { get; set; }
        [MaxLength(SD.MessageSize)]
        public string DocumentSha256Hex { get; set; }
        public int Decimals { get; set; }
        
        public virtual ICollection<SlpTransaction> Transactions { get; set; }
        // token stats
        public int? LastActiveSend { get; set; }
        [MaxLength(SD.MessageSize)]
        public string ActiveMint { get; set; }
        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal? TotalMinted { get; set; }
        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal? TotalBurned { get; set; }
        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal? CirculatingSupply { get; set; }
        public int? ValidTokenUtxos { get; set; }
        public int? ValidAddresses { get; set; }
        public int? SatoshisLockedUp { get; set; }
        public int? TxnsSinceGenesis { get; set; }
        [MaxLength(20)]
        public string MintingBatonStatus { get; set; }
        public int? BlockLastActiveSend { get; set; }
        public int? BlockLastActiveMint { get; set; }


    }
}
