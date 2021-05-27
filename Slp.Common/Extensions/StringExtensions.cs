using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Slp.Common.Extensions
{
    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) { return value; }

            return value.Substring(0, Math.Min(value.Length, maxLength));
        }
        public static byte[] ToAsciiByteArray(this string value) 
        {
            return Encoding.ASCII.GetBytes(value.ToString());
        }

        public static string MergeToDelimitedString(this IEnumerable<object> collection, char delimiter = ',')
        {
            var sb = new StringBuilder();
            int i = 0;
            foreach (var c in collection)
            {
                if (i++ > 0)
                    sb.Append(delimiter);
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static byte[] FromHex(this string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "The binary key cannot have an odd number of digits: {0}", hexString));
            }

            byte[] data = new byte[hexString.Length / 2];
            for (int index = 0; index < data.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                data[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return data;
            //if (hexString == null || !hexString.Any())
            //    return new byte[] { };
            //if (hexString.Length % 2 != 0)
            //    throw new Exception("Invalid hex string. Length must be divisible by 2");

            //var res = new byte[hexString.Length / 2];
            //for (int i = 0; i < hexString.Length; i += 2)
            //{
            //    char c1 = hexString[i];
            //    char c2 = hexString[i + 1];
            //    int upper = c1 << 4;
            //    int lower = (int)c2;
            //    res[i] = (byte)(upper + lower);
            //}
            //return res;
        }
    }
}
