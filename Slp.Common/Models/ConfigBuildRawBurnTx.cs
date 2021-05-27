using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ConfigBuildRawBurnTx
    {
        public string TokenIdHex { get; set; }
        public byte[] SlpBurnOpReturn { get; set; }
        public Utxo[] InputTokenUtxos { get; set; }
        public string BchChangeReceiverAddress { get; set; }
    }
}
