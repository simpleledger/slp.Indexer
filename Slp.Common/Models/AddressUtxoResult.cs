
using Slp.Common.Models.Enums;

namespace Slp.Common.Models
{
    public class AddressUtxoResult
    {
        public string TxId { get; set; }
        public int VOut { get; set; }
        //public string ScriptPubKey { get; set; }
        public decimal Amount { get; set; }
        public decimal? Satoshis { get; set; }
        public decimal? Value { get; set; }
        public decimal Height { get; set; }
        public decimal Confirmations { get; set; }
        //public string LegacyAddress { get; set; }
        public string CashAddress { get; set; }
        public string Wif { get; set; }
        public NBitcoin.Transaction Tx { get; set; }
        public byte[] TxBuf { get; set; }
        public TransactionDetails TransactionDetails { get; set; }
        public SlpUtxoJudgement SlpUtxoJudgement { get; set; } = SlpUtxoJudgement.UNKNOWN;
        public decimal? SlpUtxoJudgementAmount { get; set; }
        public string NftParentId { get; set; }
    }
}
