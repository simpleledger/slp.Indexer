using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slp.Common.Models
{
    public class AddressBalance
    {
        public string TokenId { get; set; }
        public decimal Balance{ get; set; }
        public string BalanceString { get; set; }
        public string SlpAddress { get; set; }
        public string BchAddress { get; set; }
        public string LegacyAddress { get; set; }
        public int DecimalCount { get; set; }
    }
}
