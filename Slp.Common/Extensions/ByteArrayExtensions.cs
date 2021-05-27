using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Slp.Common.Extensions
{
    public static class ByteArrayExtensions
    {
        public static string ToUtf8(this byte[]data)
        {
            return Encoding.UTF8.GetString(data);
        }

        public static IEnumerable<byte[]> ToBytes(this string[] hex)
        {
            return hex.Select(i => i.FromHex());
        }
        public static IEnumerable<string> ToHex(this IEnumerable<byte[]> byteArrayCollection)
        {
            return byteArrayCollection.Select(i => i.ToHex());
        }
        public static string ToSqlServerHex(this byte[] bytes)
        {
            return $"0x{bytes.ToHex()}";
        }
        public static string ToHex(this byte[] bytes)
        {
            char[] c = new char[bytes.Length * 2];
            int b;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = bytes[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c).ToLower();
        }

        public static byte[] ReverseByteArray(this byte[] byteArray)
        {
            Array.Reverse(byteArray);
            return byteArray;
        }
        //public static ushort ReadLittleEndianUInt16(this IEnumerable<byte> data)
        //{
        //    if( data == null || data.Count() > 2 )
        //        throw new Exception("Invlaid data size!");
        //    if (data.Count() == 1)
        //        return (int)data.First();
        //    if (data.Count() == 2)
        //        return BitConverter.ToInt16(data.Reverse().ToArray(), 0);
        //    return BitConverter.ToUInt16( data.Reverse().ToArray(), 0);
        //}
        public static uint ReadLittleEndianUInt32(this IEnumerable<byte> data)
        {
            if (data == null || data.Count() > 4)
                throw new Exception("Invalid data size!");
            if (data.Count() == 1)
                return (uint)data.First();
            if (data.Count() == 2)
                return BitConverter.ToUInt16(data.Reverse().ToArray(), 0);
            if (data.Count() == 4)
                return BitConverter.ToUInt32(data.Reverse().ToArray(), 0);
            throw new Exception("Invalid input");
        }

        public static uint ReadBigEndianUInt32(this IEnumerable<byte> data)
        {
            if (data == null || data.Count() > 4)
                throw new Exception("Invalid data size!");
            if (data.Count() == 1)
                return (uint)data.First();
            if (data.Count() == 2)
                return BitConverter.ToUInt16(data.ToArray(), 0);
            if (data.Count() == 4)
                return BitConverter.ToUInt32(data.ToArray(), 0);
            throw new Exception("Invalid input");
        }

        public static byte[] ToBigEndianByteArray(this ulong value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return bytes;
        }

        public static byte[] BigNumberToInt64BigEndian(this decimal value)
        {
            if (value - (decimal)((long)value)  != 0)
                throw new Exception("Value is not integer");
            if (value < 0)
                throw new Exception("Value is not positive!");
            var integerValue = (ulong)value;
            var bigEndianBytes = integerValue.ToBigEndianByteArray();            
            return bigEndianBytes;
        }

        public static ulong ToBigNumber(this byte[] amount)
        {
            if (amount.Length < 5 || amount.Length > 8)
            {
                throw new Exception("Buffer must be between 4-8 bytes in length");
            }
            var higher = amount.Take(4).ReadLittleEndianUInt32();
            var lower = amount.Skip(4).ReadLittleEndianUInt32();
            //return (new BigNumber(amount.readUInt32BE(0).toString())).multipliedBy(2 * *32).plus(amount.readUInt32BE(4).toString());
            ulong res = higher * (ulong)Math.Pow(2,32);
            return res + lower;
        }

        public static byte[] ToByteArray(this decimal dec)
        {
            //Load four 32 bit integers from the Decimal.GetBits function
            Int32[] bits = decimal.GetBits(dec);
            //Create a temporary list to hold the bytes
            List<byte> bytes = new List<byte>();
            //iterate each 32 bit integer
            foreach (Int32 i in bits)
            {
                //add the bytes of the current 32bit integer
                //to the bytes list
                bytes.AddRange(BitConverter.GetBytes(i));
            }
            //return the bytes list as an array
            return bytes.ToArray();
        }
        public static decimal ToDecimal(byte[] bytes)
        {
            //check that it is even possible to convert the array
            if (bytes.Length != 16)
                throw new Exception("A decimal must be created from exactly 16 bytes");
            //make an array to convert back to int32's
            Int32[] bits = new Int32[4];
            for (int i = 0; i <= 15; i += 4)
            {
                //convert every 4 bytes into an int32
                bits[i / 4] = BitConverter.ToInt32(bytes, i);
            }
            //Use the decimal's new constructor to
            //create an instance of decimal
            return new decimal(bits);
        }

        public static byte[] ToByteArray(this int value)
        {
            //Load four 32 bit integers from the Decimal.GetBits function
            return BitConverter.GetBytes(value);
        }
        public static byte[] ToByteArray(this short value)
        {
            //Load four 32 bit integers from the Decimal.GetBits function
            return BitConverter.GetBytes(value);
        }
    }
}
