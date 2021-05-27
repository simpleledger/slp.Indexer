
using System;
using System.Collections.Generic;
using System.Text;

namespace Slp.Common.Models
{
    public class PushDataOperation
    {
        public byte OpCode { get; set; }
        public byte[] Data { get; set; }
    }
}
