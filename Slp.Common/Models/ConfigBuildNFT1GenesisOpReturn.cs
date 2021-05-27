using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ConfigBuildNFT1GenesisOpReturn
    {
        public string Ticker { get; set; }
        public string Name { get; set; }
        public string ParentTokenIdHex { get; set; }
        public decimal ParentInputIndex { get; set; }
    }
}
