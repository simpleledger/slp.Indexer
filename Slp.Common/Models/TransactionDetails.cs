
using Slp.Common.Models.Enums;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Slp.Common.Models
{
    public class TransactionDetails
    {
        public SlpTransactionType TransactionType { get; set; }
        public string TokenIdHex { get; set; }
        public SlpVersionType VersionType { get; set; }
        public DateTime? Timestamp { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public string DocumentUri { get; set; }
        public byte[] DocumentSha256 { get; set; }
        public int Decimals { get; set; }
        public bool ContainsBaton { get; set; }
        public byte? BatonVOut { get; set; }
        public ulong? GenesisOrMintQuantity { get; set; }
        public List<decimal> SendOutputs { get; set; } = new List<decimal>();
    }
}
