using Slp.Common.Utility;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Slp.Common.Models.DbModels
{
    //public enum TokenUtxoStatus { Unspent, SpentSameToken, SpentWrongToken, SpentNotInSend, SpentNonSlp, MissingBchVOut, ExcessInputBurned }
    //public enum BatonUtxoStatus { BatonUnspent, BatonSpentInMint, BatonSpentNotInMint, BatonSpentNonSlp, BatonMissingBchVOut }
    //public enum TokenBatonStatus { NeverCreated, Alive, DeadBurned, DeadEnded, Unknown }
    /// <summary>
    /// This will store SLP Send parsed transaction data
    /// </summary>
    public class SlpTransactionInput
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }

        [ForeignKey(nameof(SlpTransaction))]
        public long SlpTransactionId { get; set; }
        public SlpTransaction SlpTransaction { get; set; }

        [ForeignKey(nameof(Address))]
        public int? AddressId { get; set; } = null;  //input can also be null for genesis txs
        public SlpAddress Address { get; set; }

        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal SlpAmount { get; set; }
        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal BlockchainSatoshis { get; set; }

        [MaxLength(SD.HashSize)]
        public byte[] SourceTxHash { get; set; }
        public int VOut { get; set; }        

       // public int? BlockHeight { get; set; }
    }
}
