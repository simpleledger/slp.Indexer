
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class VIn
    {
        public string TxId { get; set; }
        public int VOut { get; set; }
        public decimal Sequence { get; set; }
        public int N { get; set; }
        public decimal Value { get; set; }
        public string Addr { get; set; }
        public string LegacyAddress { get; set; }
        public string CashAddress { get; set; }
        public ScriptSig ScriptSig { get; set; }
        
        
    }
}
