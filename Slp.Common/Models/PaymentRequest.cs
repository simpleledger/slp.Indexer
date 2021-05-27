using System;
using System.Collections.Generic;
using System.Text;

namespace Slp.Common.Models
{
    public class PaymentRequest
    {
        public string Address { get; set; }
        public decimal? AmountBch { get; set; }
        public decimal? AmountToken { get; set; }
        public string TokenId { get; set; }
        public string[] TokenFlags { get; set; }
    }
}
