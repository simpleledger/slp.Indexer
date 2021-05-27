using Slp.Common.Extensions;
using Slp.Common.Models.Enums;
using Slp.Common.Utility;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Slp.Common.Models.DbModels
{
    /// <summary>
    /// This will store SLP Send parsed transaction data
    /// </summary>
    public class SlpTransaction
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }
        //on chain raw reference
        [MaxLength(SD.HashSize)]
        public byte[] Hash { get; set; }
        //[MaxLength(SD.HashHexSize)]
        //public string SlpTokenId { get; set; }
        [MaxLength(SD.HashSize)]
        public byte[] SlpTokenId { get; set; }
        public virtual SlpToken SlpToken { get; set; }
        
        public SlpVersionType SlpTokenType { get; set; }
        public SlpTransactionType Type { get; set; }
        
        [ForeignKey(nameof(Block))]
        public int? BlockHeight { get; set; }
        public SlpBlock Block { get; set; }        
        //public bool Unconfirmed { get; set; }        
        public enum TransactionState { SLP_UNKNOWN=0, SLP_VALID=1, SLP_INVALID=-1 }
        public TransactionState State { get; set; } = TransactionState.SLP_UNKNOWN;
        [MaxLength(SD.InvalidReasonLength)]
        public string InvalidReason { get; set; }

        // MINT TRANSACTION
        public int? MintBatonVOut { get; set; }
        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal? AdditionalTokenQuantity { get; set; }
        
        public virtual List<SlpTransactionInput> SlpTransactionInputs { get; set; } = new List<SlpTransactionInput>();
        public virtual List<SlpTransactionOutput> SlpTransactionOutputs { get; set; } = new List<SlpTransactionOutput>();

        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal? TokenInputSum { get; set; }
        [Column(TypeName = SD.AnnotationLargeInteger)]
        public decimal? TokenOutputSum { get; set; }


        public bool IsMintOrGenesis() { return Type == SlpTransactionType.MINT || Type == SlpTransactionType.GENESIS; }
        public bool IsSend() { return Type == SlpTransactionType.SEND; }
        public bool IsGenesis() { return Type == SlpTransactionType.GENESIS; }
        public bool IsMint() { return Type == SlpTransactionType.MINT; }
        public bool IsBurn() { return Type == SlpTransactionType.BURN; }
        public override string ToString()
        {
            return Hash.ToHex();
        }
    }
}
