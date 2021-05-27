using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ConfigBuildSendOpReturn
    {
        public string TokenIdHex { get; set; }
        public BigInteger[] OutputQtyArray { get; set; }
    }
}
