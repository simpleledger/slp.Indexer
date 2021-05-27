namespace Slp.Common.Models
{
    public class AddressUtxos
    {
        public AddressUtxo[] Utxos { get; set; }
        public string LegacyAddress { get; set; }
        public string CashAddress { get; set; }
        public string SlpAddress { get; set; }
        public string ScriptPubKey { get; set; }
        public string Asm { get; set; }  
    }
}
