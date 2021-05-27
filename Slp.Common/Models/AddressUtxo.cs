namespace Slp.Common.Models
{
    public class AddressUtxo
    {
        public string TxId { get; set; }
        public int VOut{ get; set; }
        public decimal Amount{ get; set; }
        public decimal Satoshis { get; set; }
        public int Height { get; set; }
        public int Confirmations { get; set; }
    }
}
