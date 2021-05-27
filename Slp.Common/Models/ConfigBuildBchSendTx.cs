using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ConfigBuildBchSendTx
    {
        public Utxo[] InputTokenUtxos { get; set; }
        public string[] BchReceiverAddressArray { get; set; }
        public BigInteger[] BchReceiverSatoshiAmounts { get; set; }
        public string BchChangeReceiverAddress { get; set; }
    }

}
