using System;
using System.Collections.Generic;
using System.Text;

namespace Slp.Common.Extensions
{
    public static class NumericExtensions
    {
        public static decimal ToTokenValue(this decimal rawvalue, int decimals)
        {
            return rawvalue / (decimal)Math.Pow(10, decimals);
        }

        public static decimal ToRawValue(this decimal tokenValue, int decimals)
        {
            return tokenValue * (decimal)Math.Pow(10, decimals);
        }
    }
}
