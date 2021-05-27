using Slp.Common.Models;
using Slp.Common.Models.DbModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Slp.Common.Interfaces
{
    public interface ISlpValidator
    {
        public delegate Task<SlpTransaction> TransactionGetter(string txs);

        void RegisterTransactionProvider(TransactionGetter transactionsGetter);
        Task<Tuple<bool,string>> IsValidAsync(string txid,string tokenHex);

        Task<SlpTransaction> GetTransactionAsync(string txid);
        void RemoveTransactionFromValidation(string txid);

        public Task<IEnumerable<string>> ValidateSlpTransactionsAsync(IEnumerable<string> txids);
    }
}
