using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slp.Common.Models
{
    public class ValidationResult
    {
        public string txid { get; set; }
        public bool Valid { get; set; }
        public string Reason { get; set; }
    }
}
