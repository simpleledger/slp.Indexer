using System;
using NBitcoin;
using NBitcoin.RPC;

namespace Slp.Common.Extensions
{
    public static class SlpAddressExtensions
    {
        public static void EnsureSlpAddress(this string address)
        {
            if (!address.IsSlpAddress())
                throw new Exception($"{address} is not slp address!");
        }
        public static bool IsSlpAddress(this string address)
        {
            try
            {
                var addr = address.DecodeSlpAddress();
                return addr.Prefix == AddressPrefix.simpleledger || addr.Prefix == AddressPrefix.slptest ||  addr.Prefix == AddressPrefix.slpreg;
            }
            catch
            {
                return false;
            }
        }
        public static bool IsBchAddress(this string address)
        {
            try
            {
                var addr = address.DecodeSlpAddress();
                return addr.Prefix == AddressPrefix.bitcoincash || addr.Prefix == AddressPrefix.bchtest || addr.Prefix == AddressPrefix.bchreg;
            }
            catch
            {
                return false;
            }
        }
        public static string ToPrefixedSlpAddress(this string address)
        {
            var data = DecodeSlpAddress(address);
            return EncodeAsSlpaddr(data);
        }

        public static string ToPrefixedBchAddress(this string address)
        {
            var data = DecodeSlpAddress(address);
            if (data.Prefix == AddressPrefix.simpleledger)
                data.Prefix = AddressPrefix.bitcoincash;
            else if (data.Prefix == AddressPrefix.slptest)
                data.Prefix = AddressPrefix.bchtest;
            else if (data.Prefix == AddressPrefix.slpreg)
                data.Prefix = AddressPrefix.bchreg;
            return EncodeAsSlpaddr(data);
        }

        public static AddressData DecodeSlpAddress(this string address)
        {
            if (address.IndexOf(':') != -1)
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
                var prefixes = new string[] { nameof(AddressPrefix.simpleledger), nameof(AddressPrefix.slpreg), nameof(AddressPrefix.slptest) };
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
        public static AddressPrefix GetBchPrefix(this RPCClient client)
        {
            return client.Network.ChainName.GetBchPrefix();
        }
        public static AddressPrefix GetBchPrefix(this NBitcoin.ChainName chainName)
        {
            if (chainName == ChainName.Mainnet)
                return AddressPrefix.bitcoincash;
            if (chainName == ChainName.Testnet)
                return AddressPrefix.bchtest;
            if (chainName == ChainName.Regtest)
                return AddressPrefix.bchreg;
            return AddressPrefix.bitcoincash;
            //throw new NotSupportedException("Invalid slp chainname.");
            //switch (networkType)
            //{
            //    case NBitcoin.NetworkType.Mainnet:
            //        return AddressPrefix.bitcoincash;
            //    case NBitcoin.NetworkType.Testnet:
            //        return AddressPrefix.bchtest;
            //    case NBitcoin.NetworkType.Regtest:
            //        return AddressPrefix.bchreg;
            //    default:
            //        return AddressPrefix.bitcoincash;
            //}
        }

        public static AddressPrefix GetSlpPrefix(this RPCClient client)
        {
            return client.Network.ChainName.GetSlpPrefix();
        }

        public static AddressPrefix GetSlpPrefix(this NBitcoin.ChainName chainName)
        {
            if( chainName == ChainName.Mainnet)
                return AddressPrefix.simpleledger;
            if (chainName == ChainName.Testnet)
                return AddressPrefix.slptest;
            if (chainName == ChainName.Regtest)
                return AddressPrefix.slpreg;
            throw new NotSupportedException("Invalid slp chainname.");
        }


        public static string ToAddress(this string address, AddressPrefix addressPrefix)
        {
            var decoded = address.DecodeBCashAddress();
            var type = decoded.Type == ScriptType.P2PKH ? ScriptType.P2PKH : ScriptType.P2PKH;
            var hash = decoded.Hash;
            return CashAddressExtensions.EncodeBCashAddress(addressPrefix, type, hash);
        }
        //public static string EncodeSlpAddress(this string address)
        //{
        //    var decoded = address.DecodePrefixedBCashAddress();
        //    return EncodeAsSlpaddr(decoded);
        //}


        static string EncodeAsSlpaddr(AddressData decoded)
        {
            var prefix = decoded.Prefix;
            var type = decoded.Type == ScriptType.P2PKH ? ScriptType.P2PKH : ScriptType.P2PKH;
            var hash = decoded.Hash;
            return CashAddressExtensions.EncodeBCashAddress(prefix, type, hash);
        }
        //private static AddressData DecodeSlpAddressWithPrefix(this string address)
        //{
        //    try
        //    {
        //        var decoded = address.DecodeBCashAddress();
        //        var hash = decoded.Hash;
        //        //var hash = Array.prototype.slice.call(decoded.hash, 0)
        //        var type = decoded.Type == ScriptType.P2PKH ? ScriptType.P2PKH : ScriptType.P2SH;
        //        switch (decoded.Prefix)
        //        {
        //            case "simpleledger":
        //                return new AddressData()
        //                {
        //                    Hash = hash,
        //                    Prefix = decoded.prefi
        //                    Network = Network.mainnet,
        //                    Type = type
        //                    //hash = hash,
        //                    //format: Format.Slpaddr,
        //                    //network: Network.Mainnet,
        //                    //    type: type
        //                };
        //            case "slptest":
        //            case "slpreg":
        //                return new AddressData() {
        //                    Hash = hash,
        //                    Format = AddressFormat.slpaddr,
        //                    Network = Network.testnet,
        //                    Type = type
        //                    //              hash: hash,
        //                    //format: Format.Slpaddr,
        //                    //network: Network.Testnet,
        //                    //type: type
        //                    //            }
        //                };
        //        }
        //    }
        //    catch (Exception)
        //    {
        //    }
        //    throw new Exception();
        //}
    }
}
