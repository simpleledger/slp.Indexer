/*
 * Copyright (c) 2019 ProtocolCash
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 *
 */

using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Slp.Common.Extensions
{
    /// <summary>
    ///     Cash Address script type version numbers (only 2 so far)
    /// </summary>
    public enum ScriptType
    {
        P2PKH = 0x0,
        P2SH = 0x8,
        // internal identifiers
        DATA = -1,
        OTHER = -2

    }
    /// <summary>
    ///     Cash Address network prefixes
    /// </summary>
    public enum AddressPrefix
    {
        bitcoincash,
        bchtest,
        bchreg,
        simpleledger, 
        slptest, 
        slpreg
    }

    public class AddressData
    {
        public AddressData(AddressPrefix prefix, ScriptType type, byte[] hash)
        {
            Prefix = prefix;
            Type = type;
            Hash = hash;
        }

        public AddressPrefix Prefix { get; set; }
        public ScriptType Type { get; set; }
        public byte[] Hash { get; set; }
    }

    // from https://github.com/cryptocoinjs/coininfo.git
    public static class BitcoinCashInfo
    {
        // base58Prefixes
        public static class Versions
        {
            public static class BIP32
            {
                public const int Private = 0x0488ade4;
                public const int Public = 0x0488b21e;
            }
            public const int BIP44 = 145;
            public const int Private = 0x80;
            public const int Public = 0x00;
            public const int ScriptHash = 0x05;
        }
    }

    /// <summary>
    ///     Decoder for Cash Addresses
    ///     https://www.bitcoincash.org/spec/cashaddr.html
    /// </summary>
    public static class CashAddressExtensions
    {
        /// <summary>
        ///     Exception specific to Cash Account formatting and input issues
        ///     - thrown by the public functions of this class
        ///     - should wrap an innerException
        /// </summary>
        public class CashAddressException : Exception
        {
            public CashAddressException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }
        static string ToBase58Check(byte[] hash, int version)
        {
            var encoder = new NBitcoin.DataEncoders.Base58CheckEncoder();
            var payload = new List<byte>
            {
                (byte)version
            }; // Buffer.allocUnsafe(21)
            payload.AddRange(hash);
            return encoder.EncodeData(payload.ToArray());
        }
        public static string ToLegacyAddress(this string address)
        {
            var addressData = address.DecodeSlpAddress();
            int version = BitcoinCashInfo.Versions.Public;
            switch (addressData.Type)
            {
                case ScriptType.P2PKH:
                    version = BitcoinCashInfo.Versions.Public;
                    break;
                case ScriptType.P2SH:
                    version = BitcoinCashInfo.Versions.ScriptHash;
                    break;
            }
            var legacyAddress = ToBase58Check(addressData.Hash, version);
            return legacyAddress;
        }



        public static string EncodeBCashAddressFromScriptPubKey(AddressPrefix prefix, Script scriptPubKey)
        {
            //var script = new Script(scriptpubkey);

            var keyId = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey);
            var scriptType = ScriptType.P2PKH;
            byte[] data;
            if (keyId == null)
            {
                var p2pkId = PayToScriptHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey);
                if (p2pkId == null)
                    throw new Exception("Failed to extract key parameters from script!");
                scriptType = ScriptType.P2SH;
                data = p2pkId.ToBytes();
            }
            else
                data = keyId.ToBytes();

            return EncodeBCashAddress(prefix, scriptType, data);
        }

        public static string EncodeBCashAddressFromInputScriptSig(AddressPrefix prefix, byte[] scriptpubkey)
        {
            var script = new Script(scriptpubkey);
            var scriptType = ScriptType.P2PKH;
            byte[] data = null;
            var id = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(script);
            if(id!=null)
                data = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(id.ScriptPubKey).ToBytes();
            if (data == null)
            {
                var scriptId = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(script);
                if (scriptId == null)
                    throw new Exception("Cannot extract sig parameters! Invalid script signature! Check Script OP codes!");
                data = scriptId.RedeemScript.Hash.ToBytes();
                scriptType = ScriptType.P2SH;
            }
            return EncodeBCashAddress(prefix, scriptType, data);
        }

        /// <summary>
        ///     Encodes a Cash Address from prefix, type, and hash160
        /// </summary>
        /// <param name="prefix">Cash Address prefix</param>
        /// <param name="scriptType">Bitcoin script type</param>
        /// <param name="hash160">Byte data from bitcoin script (usually hash160)</param>
        /// <returns>cash address formatted bitcoin address</returns>
        public static string EncodeBCashAddress(AddressPrefix prefix, ScriptType scriptType, byte[] hash160)
        {
            // validate hash length
            if (!ValidLength.Contains(hash160.Length * 8))
                throw new ArgumentException("Invalid Hash Length. Received: " + hash160.Length +
                                            ". Expected: " + string.Join(", ", ValidLength) + ".");

            // validate script type
            if (!Enum.IsDefined(typeof(ScriptType), scriptType))
                throw new ArgumentException("Invalid Script Type. Received: " + hash160.Length +
                                            ". Expected: " + string.Join(", ",
                                                Enum.GetValues(typeof(ScriptType))).Cast<ScriptType>() + ".");
            // validate prefix
            if (!Enum.IsDefined(typeof(AddressPrefix), prefix))
                throw new ArgumentException("Invalid Prefix. Received: " + prefix +
                                            ". Expected: " + string.Join(", ",
                                                Enum.GetValues(typeof(AddressPrefix))).Cast<AddressPrefix>() + ".");

            // encode and return result
            var prefixData = PrefixToUint5Array(prefix.ToString()).Concat(new byte[] { 0 });
            // var prefixData2 = prefix.ToAsciiByteArray().Concat(new byte[] { 0 });

            var versionByte = GetTypeBits(scriptType) + GetHashSizeBits(hash160);
            // convert byte array from 8bits to 5, append prefix 0 bit and version byte
            var payloadData = ConvertBits(new[] { (byte)versionByte }.Concat(hash160).ToArray(), 8, 5);
            //generate checksum from base5 array
            var checksumData = prefixData.Concat(payloadData).Concat(new byte[8]).ToArray();
            var payload = payloadData.Concat(ChecksumToUint5Array(PolyMod(checksumData))).ToArray();
            return prefix.ToString() + ":" + payload.EncodeBase32();
        }

        /// <summary>
        /// Decoded cash address with or without prefix or throw exceptions
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static AddressData DecodeBCashAddress(this string address)
        {
            if (address.IndexOf(':') != -1) //with prefix
            {
                try
                {
                    return address.DecodePrefixedBCashOrSlpAddress();
                    //return DecodeSlpAddressWithPrefix(address);
                }
                catch (Exception)
                {
                }
            }
            else
            {
                var prefixes = new string[] { nameof(AddressPrefix.bitcoincash), nameof(AddressPrefix.bchtest), nameof(AddressPrefix.bchreg) };
                for (var i = 0; i < prefixes.Length; ++i)
                {
                    try
                    {
                        var prefix = prefixes[i];
                        return (prefix + ':' + address).DecodePrefixedBCashOrSlpAddress();
                        //return DecodeSlpAddressWithPrefix(prefix + ':' + address);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            throw new Exception($"Failed to decode slp address from {address}");
        }

        /// <summary>
        ///     Decodes a CashAddress into prefix, type, and hash160
        /// </summary>
        /// <param name="address">Cash Address formatted address</param>
        /// <returns>prefix, type, hash160 bytes</returns>
        public static AddressData DecodePrefixedBCashOrSlpAddress(this string address)
        {
            //// validate prefix
            var validPrefix = false;
            foreach (var pre in Enum.GetValues(typeof(AddressPrefix)))
                if (address.StartsWith(pre + ":"))
                {
                    validPrefix = true;
                    break;
                }
            if (!validPrefix)
                throw new ArgumentException($"Invalid bcash prefix. Expected: " + EnumExtensions.EnumValuesToDelimitedString<AddressPrefix>());
            
            var pieces = address.ToLower().Split(':');
            var prefix = pieces[0];
            var payload = pieces[1].DecodeBase32();
            var payloadData = FromUint5Array(payload.Take(payload.Length - 8));
            var versionByte = payloadData[0];
            var hash = payloadData.Skip(1);
            var hashSize = GetHashSize(versionByte);
            if (hashSize != hash.Count() * 8)
                throw new Exception($"Invalid hash size: {address}");
            var type = GetType(versionByte);
            AddressPrefix addressPrefix = Enum.Parse<AddressPrefix>(prefix);
            return new AddressData(addressPrefix, type, hash.ToArray());
        }

        #region PRIVATE
        /**
         * Computes a checksum from the given input data as specified for the CashAddr
         * format: https://github.com/Bitcoin-UAHF/spec/blob/master/cashaddr.md.
         *
         * @private
         * @param {Uint8Array} data Array of 5-bit integers over which the checksum is to be computed.
         * @returns {BigInteger}
         */
        private static ulong PolyMod(IEnumerable<byte> data)
        {
            var GENERATOR = new ulong[] { 0x98f2bc8e61, 0x79b76d99e2, 0xf33e5fb3c4, 0xae2eabe2a8, 0x1e4f43e470 };
            ulong checksum = 1;
            for (var i = 0; i < data.Count(); ++i)
            {
                var value = data.ElementAt(i);
                //var topBits = checksum <<.shiftRight(35);
                var topBits = checksum >> 35;
                checksum = ((checksum & 0x07ffffffff) << 5) ^ value;
                for (var j = 0; j < GENERATOR.Length; ++j)
                {
                    //if (topBits.shiftRight(j).and(1).equals(1))
                    if( ((topBits >> j) & 1) == 1)
                    {
                        checksum ^= GENERATOR[j];
                    }
                }
            }
            return checksum ^ 1;
        }

        private static byte[] ChecksumToUint5Array(ulong checksum)
        {
            var result = new byte[8];
            for (var i = 0; i < 8; ++i)
            {
                result[7 - i] = (byte)(checksum & 31);
                checksum >>= 5;
            }
            return result;
        }

        private static byte[] PrefixToUint5Array(string prefix)
        {
            var result = new byte[prefix.Length];
            for (var i = 0; i < prefix.Length; ++i)
            {
                result[i] = (byte)(prefix[i] & 31);
            }
            return result;
        }

        static int GetTypeBits(ScriptType type)
        {
            switch (type)
            {
                case ScriptType.P2PKH:
                    return 0;
                case ScriptType.P2SH:
                    return 8;
                case ScriptType.DATA:
                    break;
                case ScriptType.OTHER:
                    break;
                default:
                    throw new Exception("Invalid type: " + type + ".");
            }
            throw new Exception("Invalid type: " + type + ".");
        }

        static int GetHashSize(byte versionByte)
        {
            return (versionByte & 7) switch
            {
                0 => 160,
                1 => 192,
                2 => 224,
                3 => 256,
                4 => 320,
                5 => 384,
                6 => 448,
                7 => 512,
                _ => throw new Exception("Invalid hash size!"),
            };
            throw new Exception("Invalid hash size!");
        }

        static byte[] FromUint5Array(IEnumerable<byte> data)
        {
            return ConvertBits(data, 5, 8, true);
        }

        static bool ValidChecksum(string prefix, byte[] payload)
        {
            var prefixData = PrefixToUint5Array(prefix).Concat( new byte[1] );
            var checksumData = prefixData.Concat(payload);
            return PolyMod(checksumData) == 0;
        }

        // Used by polymod checksum generation
        private static readonly ulong[] Generator = { 0x98f2bc8e61, 0x79b76d99e2, 0xf33e5fb3c4, 0xae2eabe2a8, 0x1e4f43e470 };
        // valid hash lengths in bits
        private static readonly int[] ValidLength = { 160, 192, 224, 256, 320, 384, 448, 512 };

        /// <summary>
        ///     Given Cash Address version byte, returns address type (Key Hash or Script Hash)
        /// </summary>
        /// <param name="versionByte"></param>
        /// <returns>P2PKH (key hash) or P2SH (script hash)</returns>
        private static ScriptType GetType(byte versionByte)
        {
            return versionByte switch
            {
                0 => ScriptType.P2PKH,
                8 => ScriptType.P2SH,
                _ => throw new ArgumentException("Invalid address type in version byte: " + versionByte),
            };
        }

        /// <summary>
        ///     Lookup size of hash in version byte table
        /// </summary>
        /// <param name="hash">Hash to encode in 8bit</param>
        /// <returns>size bits</returns>
        private static byte GetHashSizeBits(IReadOnlyCollection<byte> hash)
        {
            return (hash.Count * 8) switch
            {
                160 => 0,
                192 => 1,
                224 => 2,
                256 => 3,
                320 => 4,
                384 => 5,
                448 => 6,
                512 => 7,
                _ => throw new Exception("Invalid hash size: " + hash.Count + "."),
            };
            //return (hash.Count < 40) ?
            //    (byte)((hash.Count - 20) / 4) :
            //    (byte)(((hash.Count) - 40) / 4 + 4);
        }

        /// <summary>
        ///     Converts an array of bytes from FROM bits to TO bits
        /// </summary>
        /// <param name="data">byte array in FROM bits</param>
        /// <param name="from">word size in source array</param>
        /// <param name="to">word size for destination array</param>
        /// <param name="strictMode">leaves prefix 0 bits</param>
        /// <returns>byte array in TO bits</returns>
        private static byte[] ConvertBits(IEnumerable<byte> data, int from, int to, bool strictMode = false)
        {
            var d = data.Count() * from / (double)to;
            var length = strictMode ? (int)Math.Floor(d) : (int)Math.Ceiling(d);
            var mask = (1 << to) - 1;
            var result = new byte[length];
            var index = 0;
            var accumulator = 0;
            var bits = 0;
            foreach (var value in data)
            {
                accumulator = (accumulator << from) | value;
                bits += from;
                while (bits >= to)
                {
                    bits -= to;
                    result[index] = (byte)((accumulator >> bits) & mask);
                    ++index;
                }
            }

            if (strictMode) return result;
            if (bits <= 0) return result;

            result[index] = (byte)((accumulator << (to - bits)) & mask);
            ++index;

            return result;
        }
        #endregion
    }
}
