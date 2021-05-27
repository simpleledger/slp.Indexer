
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class VOut
    {
        public string Value { get; set; }
        public int N { get; set; }
        public ScriptPubKey ScriptPubKey { get; set; }
        public string SpentTxId { get; set; }
        public int? SpentIndex { get; set; }
        public int? SpentHeight { get; set; }
    }
}
