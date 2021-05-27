using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ConfigBuildRawMintTx
    {
        public byte[] SlpMintOpReturn { get; set; }
        public string Receiver { get; set; }
        public BigInteger[] MintReceiverSatoshis { get; set; }
        public string BatonReceiverAddress { get; set; }
        public BigInteger[] BatonReceiverSatoshis { get; set; }
        public string BchChangeReceiverAddress { get; set; }
        public Utxo[] InputBatonUtxos { get; set; }
        public decimal? ExtraFee { get; set; }
        public bool? DisableBchChangeOutput { get; set; }
    }
}
