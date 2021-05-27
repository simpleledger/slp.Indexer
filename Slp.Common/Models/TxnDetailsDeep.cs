
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class TxnDetailsDeep
    {
        public string TxId { get; set; }
        public uint Version { get; set; }
        public uint LockTime { get; set; }
        public VIn[] VIn { get; set; }
        public VOut[] VOut { get; set; }
        public string BlockHash { get; set; }
        public decimal BlockHeight { get; set; }
        public decimal Confirmation { get; set; }
        public int Time { get; set; }
        public int BlockTime { get; set; }
        public bool IsCoinbase { get; set; }
        public decimal ValueOut { get; set; }
        public decimal ValueIn { get; set; }
        public decimal Size { get; set; }
        public decimal Fees { get; set; }
    }
}
