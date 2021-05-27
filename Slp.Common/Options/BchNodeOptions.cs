using NBitcoin;
using NBitcoin.RPC;
using Slp.Common.Utility;
using System;
using System.Net;

namespace Slp.Common.Options
{
    public class BchNodeOptions
    {
        public const string Position = "BchNode";
        public string User { get; set; }
        public string Password { get; set; }
        public string Url { get; set; }
        public string Type { get; set; }


        public RPCClient CreateClient()
        {
            var user = User ?? SD.BchNodeUser;
            var password = Password ??  SD.BchNodePassword;
            var url = Url ?? SD.BchNodeUrl;
            var nodeType = Type ?? SD.BchNodeType;
            var network = NBitcoin.Altcoins.BCash.Instance.GetNetwork(new ChainName(nodeType));
            return new RPCClient(new NetworkCredential(user, password), new Uri(url), network);
        }
    }
}
