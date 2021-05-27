using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slp.Common.Models
{
    public class TokenBalanceData
    {
        public decimal TokenBalance { get; set; }
        public string TokenBalanceString { get; set; }
        public string SlpAddress{ get; set; }
        public string TokenId { get; set; }
    }
}
