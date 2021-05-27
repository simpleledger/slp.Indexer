using Slp.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slp.Common.Models
{
    public class TxDetails
    {
        public TxnDetailsDeep RetData { get; set; }
        public TokenInfo TokenInfo { get; set; }
    }
}
