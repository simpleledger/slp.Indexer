using Slp.Common.Models;
using Slp.Common.Models.DbModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Slp.Common.Interfaces
{
    public interface ISlpDataService
    {
        Task<string> GetTokenActiveMintTxAsync(string tokenId);
        Task<int> GetTokenTransactionCount(string tokenId);
        Task<int> GetValidTokenUtxoCount(string tokenId);
        Task<int> GetValidTokenAddressesCount(string tokenId);
        Task<decimal> GetTokenTotalMintedCount(string tokenId);
        Task<decimal> GetTokenTotalBurned(string tokenId);
        Task<TokenViewModel[]> GetTokenInformationAsync(string[] tokenIds);
        Task<AddressBalance> GetTokenBalancesAsync(string slpAddress, string tokenId);
        Task<AddressBalance[]> GetAddressBalancesAsync(string address);
        Task<TokenBalanceData[]> GetTokenBalancesAsync(string tokenId);
        Task<ValidationResult> ValidateTransactionAsync(string txId);
        Task<ValidationResult[]> ValidateTransactionsAsync(TxIds txIds);
        Task<TxDetails> GetTransactionDetails(string txId);
        Task<TxTokenDetails[]> GetTransactions(string tokenId, string address);
        Task<TxBurn[]> GetBurnTotal(string[] txIds);
    }
}
