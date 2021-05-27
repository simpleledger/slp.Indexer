using Slp.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slp.Common.Models
{
    public class TxBurn 
    {
        public string TransactionId { get; set; }
        public decimal? InputTotal { get; set; }
        public decimal? OutputTotal { get; set; }
        public decimal? BurnTotal { get; set; }
    }
}
