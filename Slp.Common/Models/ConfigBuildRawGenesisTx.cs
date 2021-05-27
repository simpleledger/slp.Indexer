using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ConfigBuildRawGenesisTx
    {
        public byte[] SlpGenesisOpReturn { get; set; }
        public string MintReceiverAddress { get; set; }
        public decimal? MintReceiverSatoshis { get; set; }
        public string BatonReceiverAddress { get; set; }
        public decimal? BatonReceiverSatoshis { get; set; }
        public string BchChangeReceiverAddress { get; set; }
        public Utxo[] InputUtxos { get; set; }
        public string[] AllowedTokenBurning { get; set; }
        public string ParentTokenIdHex { get; set; }
    }
}
