using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ConfigBuildGenesisOpReturn
    {
        public string Ticker { get; set; }
        public string Name { get; set; }
        public string DocumentUri { get; set; }
        public byte[] Hash { get; set; }
        public decimal Decimals { get; set; }
        public decimal? BatonVOut { get; set; }
        public BigInteger InitialQuantity { get; set; }
    }
}
