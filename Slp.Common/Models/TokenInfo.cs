using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slp.Common.Models
{
    public class TokenInfo
    {
        public string TransactionType { get; set; }
        public int VersionType { get; set; }
        public string TokenIdHex { get; set; }
        public List<string> SendOutputs { get; set; } = new List<string>();
        public bool TokenIsValid { get; set; }
    }
}
