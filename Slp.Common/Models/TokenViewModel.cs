using Slp.Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Slp.Common.Models
{
    public class TokenViewModel
    {
        public int Decimals { get; set; }
        public string DocumentUri { get; set; }
        public string DocumentHash { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public bool ContainsBaton { get; set; }
        public string Id { get; set; }
        [JsonIgnore]
        public decimal InitialTokenQtyInSatoshis { get; set; }
        public decimal InitialTokenQty { get { return InitialTokenQtyInSatoshis.ToTokenValue(Decimals); } }
        public int BlockCreated { get; set; }
        public decimal Quantity { get { return InitialTokenQty; } }
        public decimal LastActiveSend { get; set; }
        public string ActiveMint { get; set; }
        public int VersionType { get; set; }
        public int TimestampUnix { get; set; }
        public string Timestamp { get; set; }
        public decimal TotalMinted { get; set; }
        public decimal TotalBurned  { get; set; }
        public decimal CirculatingSupply { get; set; }
        public int ValidTokenUtxos { get; set; }
        public int ValidAddresses { get; set; }
        public decimal SatoshisLockedUp { get; set; }
        public decimal TxnsSinceGenesis { get; set; }
        public string MintingBatonStatus { get; set; }
        public int? BlockLastActiveSend { get; set; }
        public int? BlockLastActiveMint { get; set; }
    }
}
