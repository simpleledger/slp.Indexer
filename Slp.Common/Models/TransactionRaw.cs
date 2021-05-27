using System;
using System.Collections.Generic;
using System.Text;

namespace Slp.Common.Models
{
    public class TransactionInputRaw
    {
        public string PreviousTxHash { get; set; }
        public int PreviousTxOutputIndex { get; set; }
        public byte[] ScriptSig { get; set; }
        public string SequenceNo { get; set; }
        public bool Incomplete { get; set; }
        public ulong? Satoshis { get; set; }
    }
    public class TransactionOutputRaw
    {
        public byte[] ScriptPubKey { get; set; }
        public ulong Value { get; set; }        
    }

    public class TransactionRaw
    {
        public ulong Version { get; set; }
        public DateTime Locktime { get; set; }
        public TransactionInputRaw[] Inputs { get; set; }
        public TransactionOutputRaw[] Outputs { get; set; }
    }
}
