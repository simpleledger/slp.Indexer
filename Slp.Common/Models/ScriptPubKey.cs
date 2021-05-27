
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class ScriptPubKey
    {
        public string Hex { get; set; }
        public string Asm { get; set; }
        public string[] Addresses { get; set; }
        public string Type { get; set; }
        public string[] CashAddrs { get; set; } = new string[] { };
    }
}
