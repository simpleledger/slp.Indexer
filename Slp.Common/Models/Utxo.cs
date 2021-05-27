
using Slp.Common.Models.Enums;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class Utxo
    {
        public string TxId { get; set; }
        public int VOut { get; set; }
        public decimal Satoshis { get; set; }
        public string Wif { get; set; }
        public TransactionDetails TransactionDetails { get; set; }
        public SlpUtxoJudgement SlpUtxoJudgement { get; set; }
        public decimal SlpUtxoJudgementAmount { get; set; }
    }
}
