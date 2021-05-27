using Slp.Common.Models.Enums;
using Slp.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Slp.Common.Models
{
    public class SlpTxOutput
    {
        public string Address { get; set; }
        public string Amount { get; set; }
    }
    public class Details
    {
        public int Decimals { get; set; }
        public string TokenIdHEx { get; set; }
        public string TransactionType { get; set; }
        public int VersionType { get; set; }
        public string DocumentUri { get; set; }
        public string DocumentSha256Hex { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public int? TxnBatonVOut { get; set; }
        public bool TxnContainsBaton { get; set; }
        public List<SlpTxOutput> Outputs { get; set; }
    }
    public class TokenDetails
    {
        public bool Valid { get; set; }
        public Details Detail { get;set;}
        public string InvalidReason { get; set; }
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 79;
    }

    public class TxTokenDetails
    {
        public string TxId { get; set; }
        public TokenDetails TokenDetails { get; set; }
        
    }
}
