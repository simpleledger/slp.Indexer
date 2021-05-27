using System;
using System.Linq;
using System.Text;

namespace Slp.Common.Extensions
{
    public static class EnumExtensions
    {
        public static byte[] ToAsciiByteArray<T>(this T enumValue) where T: Enum
        {
            return Encoding.ASCII.GetBytes(enumValue.ToString());
        }

        public static string EnumValuesToDelimitedString<T>() where T : Enum
        {
            var values = Enum.GetValues(typeof(T));
            var res = values.Cast<T>().Select(s => s.ToString()).MergeToDelimitedString();
            return res;
            
        }
    }
}
