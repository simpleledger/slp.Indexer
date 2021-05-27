using Slp.Common.Models;
using Slp.Common.Models.DbModels;
using Slp.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Slp.Common.Services
{
    public class SlpNullValidationService : ISlpValidator
    {
        public SlpNullValidationService()
        {
        }

        #region ISlpValidator
        public void RegisterTransactionProvider(ISlpValidator.TransactionGetter transactionGetter)
        {
            throw new NotSupportedException("Null validator prevents validation services. User local validation or remote validation service!");
        }
        public Task<Tuple<bool, string>> IsValidAsync(string txid, string tokexHex)
        {
            return Task.FromResult(new Tuple<bool, string>(false, "Null validator prevernts validation services"));
        }
        public Task<IEnumerable<string>> ValidateSlpTransactionsAsync(IEnumerable<string> txids)
        {
            throw new NotSupportedException("Null validator prevents validation services. User local validation or remote validation service!");
        }

        public void RemoveTransactionFromValidation(string txid)
        {
            throw new NotSupportedException("Null validator prevents validation services. User local validation or remote validation service!");
        }
        public Task<SlpTransaction> GetTransactionAsync(string txId)
        {
            throw new NotSupportedException("Null validator prevents validation services. User local validation or remote validation service!");
        }
        #endregion

    }
}
