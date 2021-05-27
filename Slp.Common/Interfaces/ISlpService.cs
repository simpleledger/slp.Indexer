using Slp.Common.Models;
using Slp.Common.Models.DbModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Slp.Common.Interfaces
{
    public interface ISlpService
    {
        TransactionDetails GetTransactionDetails(byte[] scriptPubKeyAtIndex0);
        SlpTransaction GetSlpTransaction(NBitcoin.Transaction tr);
        Task<Dictionary<string, BalancesResult>> GetAllSlpBalancesAndUtxosAsync(params string[] addresses);

        /// <summary>
        /// Send specified amount of slp token to the receiver
        /// </summary>
        /// <param name="tokenHex">Token id hex string</param>
        /// <param name="tokenDecimals">Token decimals</param>
        /// <param name="from">Slp from address</param>
        /// <param name="to">Slp to address</param>
        /// <param name="amount">Token amount</param>
        /// <param name="wif">Wallet import format(private key)</param>
        /// <returns></returns>
        Task<string> SendTokensExactAsync(string tokenHex, string from, string to, decimal amount, string wif);
        /// <summary>
        /// Sends tokens from array of address to array of addresses.
        /// </summary>
        /// <param name="tokenHex">Token id hex string</param>
        /// <param name="tokenDecimals">Token decimals</param>
        /// <param name="collectingAddress">Collecting address where remaining tokens are sent. Also used as a funding address for slp transactions</param>
        /// <param name="froms">Array of sending addresses</param>
        /// <param name="tos">Array of receiver addresses </param>
        /// <param name="amounts">Array of amounts to send to</param>
        /// <param name="wifs">Array of private keys that controls each from address</param>
        /// <returns></returns>
        Task<string> SendTokensManyManyAsync(string tokenHex, string collectingAddress, string[] froms, string[] tos, decimal[] amounts, string[] wifs);

        Task<string> SimpleTokenSendAsync(string  tokenIdHex, decimal[] sendAmounts, AddressUtxoResult[] inputUtxos,
            string[] tokenReceiverAddresses, string changeReceiverAddress, 
            NonTokenOutput[] requiredNonTokenOutputs
            );

        /// <summary>
        /// SEnds token defined by inputUtxos to token receiver addresses. Token change goes to 
        /// </summary>
        /// <param name="tokenIdHex">Token id</param>
        /// <param name="sendAmounts">Amount that will be send to outputs</param>
        /// <param name="inputUtxos">Input slp and bch addresses that will  be spent</param>
        /// <param name="tokenReceiverAddresses"></param>
        /// <param name="tokenChangeReceiverAddress"></param>
        /// <param name="bchChangeReceiverAddress"></param>
        /// <param name="requiredNonTokenOutputs"></param>
        /// <returns></returns>
        Task<NBitcoin.Transaction> SimpleTokenSendAsync2(string tokenIdHex, decimal[] sendAmounts, AddressUtxoResult[] inputUtxos,
            string[] tokenReceiverAddresses, string tokenChangeReceiverAddress, string bchChangeReceiverAddress,
            NonTokenOutput[] requiredNonTokenOutputs
            );
        NBitcoin.Transaction PrepareTokenSendTransaction(
            string tokenIdHex, 
            decimal[] sendAmounts, 
            AddressUtxoResult[] inputUtxos,
            string[] tokenReceiverAddresses, 
            string tokenChangeReceiverAddress, 
            string bchChangeReceiverAddress,
            NonTokenOutput[] requiredNonTokenOutputs
            );    
    }
}
