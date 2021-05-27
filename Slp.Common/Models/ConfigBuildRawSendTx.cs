using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ConfigBuildRawSendTx
    {
        public byte[] SlpSendOpReturn { get; set; }
        public Utxo[] InputTokenUtxos { get; set; }
        public string[] TokenReceiverAddressArray { get; set; }
        public string BchChangeReceiverAddress { get; set; }
        public string TokenChangeReceiverAddress { get; set; }
        public NonTokenOutput[] RequiredNonTokenOutputs { get; set; }
        public decimal? ExtraFee { get; set; }
    }
}
