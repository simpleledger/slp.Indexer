using Slp.Common.Utility;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Slp.Common.Models.DbModels
{
    /// <summary>
    /// This will store SLP Send parsed transaction data
    /// </summary>
    public class SlpTransactionOutput
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }        
        [MaxLength(SD.AddressSize)]
        //public string  Address { get; set; }

        [ForeignKey(nameof(Address))]
        public int AddressId { get; set; }
        public SlpAddress Address { get; set; }

        public int VOut { get; set; }
        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal Amount { get; set; }
        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal BlockchainSatoshis { get; set; }

        //[MaxLength(SD.HashHexSize)]
        //public string SlpTransactionHex { get; set; }

        [ForeignKey(nameof(NextInput))]
        public long? NextInputId { get; set; }
        public virtual SlpTransactionInput NextInput { get; set; }

        [ForeignKey(nameof(SlpTransaction))]
        public long SlpTransactionId { get; set; }
        public virtual SlpTransaction SlpTransaction { get; set; }

       // public int? BlockHeight { get; set; }
    }
}
