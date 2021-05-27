using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ConfigBuildRawNFT1GenesisTx
    {
        public byte[] SlpNFT1GenesisOpReturn { get; set; }
        public string MintReceiverAddress { get; set; }
        public BigInteger MintReceiverSatoshis { get; set; }
        public string BchChangeReceiverAddress { get; set; }
        public Utxo[] InputUtxos { get; set; }
        public string ParentTokenIdHex { get; set; }
    }
}
