using System;
using System.Collections.Generic;
using System.Text;

namespace Slp.Common.Models
{
    public class BalancesResult
    {
        public decimal SatoshisAvailableBch { get; set; }
        public decimal SatoshisInSlpBaton { get; set; }
        public decimal SatoshisInSlpToken { get; set; }
        public decimal SatoshisInInvalidTokenDAG { get; set; }
        public decimal SatoshisInInvalidBatonDag { get; set; }
        public decimal SatoshisInUnknownTokenType { get; set; }
        public Dictionary<string, decimal> SlpTokenBalances { get; set; } = new Dictionary<string, decimal>();
        public Dictionary<string, Dictionary<string,decimal>> NftParentChildBalances { get; set; } = new Dictionary<string, Dictionary<string, decimal>>();
        public Dictionary<string, List<AddressUtxoResult>> SlpTokenUtxos { get; set; } = new Dictionary<string, List<AddressUtxoResult>>();
        public Dictionary<string, List<AddressUtxoResult>> SlpBatonUtxos { get; set; } = new Dictionary<string, List<AddressUtxoResult>>();
        public List<AddressUtxoResult> NonSlpUtxos { get; set; } = new List<AddressUtxoResult>();
        public List<AddressUtxoResult> InvalidTokenUtxos { get; set; } = new List<AddressUtxoResult>();
        public List<AddressUtxoResult> InvalidBatonUtxos { get; set; } = new List<AddressUtxoResult>();
        public List<AddressUtxoResult> UnknownTokenTypeUtxos { get; set; } = new List<AddressUtxoResult>();            
    }
}
