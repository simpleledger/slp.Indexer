using Slp.Common.Extensions;
using Slp.Common.Models;
using Slp.Common.Models.Enums;
using Slp.Common.Models.DbModels;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Logging;


using Slp.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slp.Common.Services
{
    public class SlpLocalValidationService : ISlpValidator
    {
        private readonly ILogger<SlpLocalValidationService> _log;
        public SlpLocalValidationService(ILogger<SlpLocalValidationService> log)
        {
            _log = log;
        }

        #region ISlpValidator
        private ISlpValidator.TransactionGetter _transactionGetter;
        public void RegisterTransactionProvider(ISlpValidator.TransactionGetter transactionGetter)
        {
            _transactionGetter = transactionGetter;
        }
        public async Task<Tuple<bool, string>> IsValidAsync(string txid, string tokexHex)
        {
            // this.logger.log("SLPJS Validating: " + txid);
            var valid = await IsValidSlpTransactionAsync(txid, tokexHex, null);
            var reason = "Valid";
            if (!valid)
                reason = this.cachedValidations[txid].InvalidReason;
            return new Tuple<bool, string>(valid, reason);
        }
        public async Task<IEnumerable<string>> ValidateSlpTransactionsAsync(IEnumerable<string> txids)
        {
            var res = new List<string>();
            foreach (var txid in txids)
            {
                var tx = await GetTransactionAsync(txid);
                if (tx == null)
                    continue;
                var isValidState = await IsValidAsync(txid, null);
                if (isValidState.Item1)
                    res.Add(txid);
            }
            return res;
        }

        public void RemoveTransactionFromValidation(string txid)
        {
            if (this.cachedValidations.TryGetValue(txid, out TransactionValidationState state))
            {
                this.cachedValidations.Remove(txid);
                foreach (var cv in this.cachedValidations)
                {
                    var p = cv.Value.Parents.Find(p => p.TrHex == txid);
                    if (p != null)
                        cv.Value.Parents.Remove(p);
                }
            }
        }
        public Task<SlpTransaction> GetTransactionAsync(string txId)
        {
            if (_transactionGetter == null)
                throw new NullReferenceException("Local validator must have _transactionsGetter set. Call RegisterTransactionProvider before starting validation!");
            return _transactionGetter(txId);
        }
        #endregion

        protected Dictionary<string, TransactionValidationState> cachedValidations = new Dictionary<string, TransactionValidationState>();

        #region Implementation
        //this must be implemented in module that provides some data backend 
       
        public async Task WaitForCurrentValidationProcessingAsync(string txid)
        {
            this.cachedValidations.TryGetValue(txid, out TransactionValidationState transactionValidationState);

            while (true)
            {
                if (transactionValidationState.IsValid.HasValue)
                {
                    transactionValidationState.Waiting = false;
                    break;
                }
                await Task.Delay(10);
            }
        }

        public class ParentState
        {
            public string TrHex { get; set; }
            public int VOut { get; set; }
            public SlpVersionType VersionType { get; set; }
            public bool? IsValid { get; set; } = false;
            public decimal? InputQty { get; set; }
        }

        public class TransactionValidationState
        {
            public bool? IsValid { get; set; } = null;
            public List<ParentState> Parents { get; set; }
            public SlpTransaction Transaction { get; set; }
            public string InvalidReason { get; set; }
            public bool Waiting { get; set; } = false;
        }
        public async Task<bool> IsValidSlpTransactionAsync(string txid, string tokenIdFilter, SlpVersionType? slpVersionType)
        {
            //if(cachedValidations.ContainsKey(txid) )
            //    cachedValidations.Remove(txid);
            // Check to see if this txn has been processed by looking at shared cache, if doesn't exist then download txn.
            if (!cachedValidations.TryGetValue(txid, out TransactionValidationState transactionValidationState))
            {
                cachedValidations.Add(txid, transactionValidationState = new TransactionValidationState()
                {
                    IsValid = false,
                    Parents = new List<ParentState>(),
                    Transaction = null,
                    InvalidReason = null,
                    Waiting = false
                });
                transactionValidationState.Transaction = await GetTransactionAsync(txid);
                if (transactionValidationState.Transaction == null)
                    throw new NullReferenceException($"Failed to retrieve transaction {txid}.");

                // if transaction is loaded from our db and was already validated then stop validation here
                // our DAG was already processed and there is no need to check again every parent to genesis
                if (transactionValidationState.Transaction.State == SlpTransaction.TransactionState.SLP_VALID)
                {
                    transactionValidationState.IsValid = true;
                    return transactionValidationState.IsValid.Value;
                }
                else if (transactionValidationState.Transaction.State == SlpTransaction.TransactionState.SLP_INVALID)
                {
                    transactionValidationState.IsValid = false;
                    return transactionValidationState.IsValid.Value;
                }
            }
            // Otherwise, we can use the cached result as long as a special filter isn't being applied.
            else if (transactionValidationState.IsValid.HasValue && tokenIdFilter==null && slpVersionType==null)  //if validity was determined
                return transactionValidationState.IsValid.Value;
            // Handle case where txid is already in the process of being validated from a previous call
            if (transactionValidationState.Waiting)
            {
                await WaitForCurrentValidationProcessingAsync(txid);
                if (transactionValidationState.IsValid.HasValue) {
                    return transactionValidationState.IsValid.Value;
                }
            }
            transactionValidationState.Waiting = true;
            // Check SLP message validity - here we already have parsed slp transaction parsed from our node
            var slpTx = transactionValidationState.Transaction;            
            // Check for tokenId filter
            if (tokenIdFilter != null && slpTx.SlpTokenId.ToHex() != tokenIdFilter)
            {
                transactionValidationState.Waiting = false;
                transactionValidationState.InvalidReason = "Validator was run with filter only considering tokenId " + tokenIdFilter + " as valid.";
                return false; // Don't save boolean result to cache incase cache is ever used without tokenIdFilter.
            }
            else
            {
                if (transactionValidationState.IsValid != false)
                    transactionValidationState.InvalidReason = null;
            }

            // Check specified token type is being respected
            if (slpVersionType.HasValue && slpTx.SlpTokenType != slpVersionType)
            {
                transactionValidationState.IsValid = null;
                transactionValidationState.Waiting = false;
                transactionValidationState.InvalidReason = "Validator was run with filter only considering token type: " + slpVersionType.ToString() + " as valid.";
                return false; // Don't save boolean result to cache incase cache is ever used with different token type.
            }
            else
            {
                if (transactionValidationState.IsValid != false)
                    transactionValidationState.InvalidReason = null;
            }

            // Check DAG validity
            if (slpTx.Type == SlpTransactionType.GENESIS)
            {
                // Check for NFT1 child (type 0x41)
                if (slpTx.SlpTokenType == SlpVersionType.TokenVersionType1_NFT_Child)
                {
                    // An NFT1 parent should be provided at input index 0,
                    // so we check this first before checking the whole parent DAG
                    if (!slpTx.SlpTransactionInputs.Any())
                        throw new Exception("NFT1 parent is missing input at index 0. ");
                    var input_txid = slpTx.SlpTransactionInputs.First().SourceTxHash.ToHex(); // SlpSourceTransactionHex;
                    var inputSlpTransaction = await GetTransactionAsync(input_txid);
                    if (inputSlpTransaction == null)
                    {
                        transactionValidationState.Waiting = false;
                        transactionValidationState.InvalidReason = "Slp parent transaction for NFT1 Child does not exist!";
                    }

                    if (inputSlpTransaction.SlpTokenType != SlpVersionType.TokenVersionType1_NFT_Parent)
                    {
                        transactionValidationState.IsValid = false;
                        transactionValidationState.Waiting = false;
                        transactionValidationState.InvalidReason = "NFT1 child GENESIS does not have a valid NFT1 parent input.";
                        return transactionValidationState.IsValid.Value;
                    }
                    // Check that the there is a burned output >0 in the parent txn SLP message
                    if (inputSlpTransaction.Type == SlpTransactionType.SEND &&
                        !(inputSlpTransaction.SlpTransactionOutputs.ElementAtOrDefault(1)?.Amount > 0)
                        )
                    {
                        transactionValidationState.IsValid = false;
                        transactionValidationState.Waiting = false;
                        transactionValidationState.InvalidReason = "NFT1 child's parent has SLP output that is not greater than zero.";
                        return transactionValidationState.IsValid.Value;
                    }
                    else if (
                        (inputSlpTransaction.Type == SlpTransactionType.GENESIS ||
                        inputSlpTransaction.Type == SlpTransactionType.MINT)
                        &&
                        !(inputSlpTransaction.AdditionalTokenQuantity > 0)
                        )
                    {
                        transactionValidationState.IsValid = false;
                        transactionValidationState.Waiting = false;
                        transactionValidationState.InvalidReason = "NFT1 child's parent has SLP output that is not greater than zero.";
                        return transactionValidationState.IsValid.Value;
                    }
                    // Continue to check the NFT1 parent DAG
                    var nft_parent_dag_validity = await IsValidSlpTransactionAsync(input_txid, null, SlpVersionType.TokenVersionType1_NFT_Parent);
                    transactionValidationState.IsValid = nft_parent_dag_validity;
                    transactionValidationState.Waiting = false;
                    if (!nft_parent_dag_validity)
                        transactionValidationState.InvalidReason = "NFT1 child GENESIS does not have valid parent DAG.";
                    return transactionValidationState.IsValid.Value;
                }
                // All other supported token types (includes 0x01 and 0x81)
                // No need to check type here since op_return parser throws on other types.
                else
                {
                    transactionValidationState.IsValid = true;
                    transactionValidationState.Waiting = false;
                    return transactionValidationState.IsValid.Value;
                }
            }
            else if (slpTx.Type == SlpTransactionType.MINT)
            {
                for (var i = 0; i < slpTx.SlpTransactionInputs.Count; i++)
                {
                    var input_txid = slpTx.SlpTransactionInputs.ElementAt(i).SourceTxHash.ToHex();// SlpSourceTransactionHex;
                    var inputSlpTransaction = await GetTransactionAsync(input_txid);
                    if (inputSlpTransaction == null || inputSlpTransaction.Type == SlpTransactionType.BURN) //not a slp tranction - bch transaction are ignored
                        continue;
                     
                    if (inputSlpTransaction.SlpTokenId.ToHex() == slpTx.SlpTokenId.ToHex()) 
                    {
                        if (inputSlpTransaction.Type == SlpTransactionType.GENESIS || 
                            inputSlpTransaction.Type == SlpTransactionType.MINT)
                        {
                            if (inputSlpTransaction.MintBatonVOut.HasValue && 
                                slpTx.SlpTransactionInputs.ElementAtOrDefault(i).VOut == inputSlpTransaction.MintBatonVOut.Value )
                            {
                                var trHex = slpTx.SlpTransactionInputs.ElementAt(i).SourceTxHash.ToHex(); //.SlpSourceTransactionHex;
                                var vout = inputSlpTransaction.MintBatonVOut.Value;
                                if (transactionValidationState.Parents.Any(p => p.TrHex == trHex && p.VOut == vout))
                                {
                                    _log.LogDebug("MINT transaction {0} validation tried to add same parent {1},{2} twice.", slpTx.Hash.ToHex(), inputSlpTransaction.Hash.ToHex(), inputSlpTransaction.MintBatonVOut);
                                    continue;
                                }
                                transactionValidationState.Parents.Add(
                                    new ParentState
                                    {
                                        TrHex =  trHex,
                                        VOut = vout,
                                        VersionType = inputSlpTransaction.SlpTokenType,
                                        InputQty = null,
                                        IsValid = null
                                    });
                            }
                        }
                    }
                }
                if (transactionValidationState.Parents.Count < 1)
                {
                    transactionValidationState.IsValid = false;
                    transactionValidationState.Waiting = false;
                    transactionValidationState.InvalidReason = "MINT transaction must have at least 1 candidate baton parent input.";
                    return transactionValidationState.IsValid.Value;
                }
            }
            else if (slpTx.Type == SlpTransactionType.SEND)
            {
                var tokenOutQty = slpTx.SlpTransactionOutputs.Sum(t => t.Amount); 
                var tokenOutQty1 = slpTx.TokenOutputSum;
                var tokenInQty = 0M;
                for (var i = 0; i < slpTx.SlpTransactionInputs.Count; i++) {

                    var input_txid = slpTx.SlpTransactionInputs.ElementAt(i).SourceTxHash.ToHex();
                    var inputSlpTransaction = await GetTransactionAsync(input_txid);

                    if (inputSlpTransaction == null || inputSlpTransaction.Type == SlpTransactionType.BURN)
                        continue; //skip all tx not in cache and all burn transactions
                    if (inputSlpTransaction.SlpTokenId.ToHex() == slpTx.SlpTokenId.ToHex())
                    {
                        if (inputSlpTransaction.Type == SlpTransactionType.SEND)
                        {
                            var outputIndex = slpTx.SlpTransactionInputs.ElementAt(i).VOut;
                            if (outputIndex <= inputSlpTransaction.SlpTransactionOutputs.Count - 1)
                            {
                                var outputAmount = inputSlpTransaction.SlpTransactionOutputs.ElementAt(outputIndex).Amount;
                                tokenInQty += outputAmount;

                                var trHex = slpTx.SlpTransactionInputs.ElementAt(i).SourceTxHash.ToHex();
                                var vout = slpTx.SlpTransactionInputs.ElementAt(i).VOut;
                                if (transactionValidationState.Parents.Any(p => p.TrHex == trHex && p.VOut == vout))
                                {
                                    _log.LogDebug("SEND transaction {0} validation tried to add same parent {1},{2} twice.", 
                                        slpTx.Hash.ToHex(), 
                                        trHex, 
                                        vout);
                                    continue;
                                }
                                transactionValidationState.Parents.Add(
                                        new ParentState()
                                        {
                                            TrHex = trHex,
                                            VOut = vout,
                                            VersionType = inputSlpTransaction.SlpTokenType,
                                            IsValid = null,
                                            InputQty = inputSlpTransaction.TokenOutputSum
                                        }
                                    );
                            }
                        }
                        else if (inputSlpTransaction.Type == SlpTransactionType.GENESIS || 
                            inputSlpTransaction.Type == SlpTransactionType.MINT)
                        {
                            var outputIndex = slpTx.SlpTransactionInputs.ElementAt(i).VOut;
                            if (outputIndex == 1)
                            {
                                if (!inputSlpTransaction.AdditionalTokenQuantity.HasValue)
                                    throw new Exception("Mint or genesis transaction must have token quantity set.");
                                tokenInQty += inputSlpTransaction.AdditionalTokenQuantity.Value;
                                var trHex = slpTx.SlpTransactionInputs.ElementAt(i).SourceTxHash.ToHex();
                                var vout = slpTx.SlpTransactionInputs.ElementAt(i).VOut;
                                if (transactionValidationState.Parents.Any(p => p.TrHex == trHex && p.VOut == vout))
                                {
                                    _log.LogDebug("SEND transaction {0} validation tried to add same parent {1},{2} twice.",
                                        slpTx.Hash.ToHex(),
                                        trHex,
                                        vout);
                                    continue;
                                }
                                transactionValidationState.Parents.Add(
                                        new ParentState()
                                        {
                                            TrHex = trHex,
                                            VOut = vout,
                                            VersionType = inputSlpTransaction.SlpTokenType,
                                            IsValid = null,
                                            InputQty = inputSlpTransaction.AdditionalTokenQuantity
                                        }
                                    );
                            }
                        }
                    }
                }
                // Check token inputs are greater than token outputs (includes valid and invalid inputs)
                if (tokenOutQty > tokenInQty)
                {
                    transactionValidationState.IsValid = false;
                    transactionValidationState.Waiting = false;
                    transactionValidationState.InvalidReason = "Token outputs are greater than possible token inputs.";
                    return transactionValidationState.IsValid.Value;
                }
            }

            // Set validity validation-cache for parents, and handle MINT condition with no valid input
            // we don't need to check proper token id since we only added parents with same ID in above steps.
            //var parentTxids = transactionValidationState.Parents.Select(p => p.TrHex);
            var parentTxids = transactionValidationState.Parents.Select(p => p.TrHex).ToArray();
            for (var i = 0; i < parentTxids.Length; i++) 
            {
                var valid = await this.IsValidSlpTransactionAsync(parentTxids.ElementAt(i), null, null);
                transactionValidationState.Parents.Where(p => p.TrHex == parentTxids[i]).ToList().ForEach(p => p.IsValid = valid);
            }
            // Check MINT for exactly 1 valid MINT baton
            if (transactionValidationState.Transaction.Type == SlpTransactionType.MINT)
            {
                if (transactionValidationState.Parents.Where(p => p.IsValid == true && p.InputQty == null).Count() != 1)
                {
                    transactionValidationState.IsValid = false;
                    transactionValidationState.Waiting = false;
                    transactionValidationState.InvalidReason = "MINT transaction with invalid baton parent.";
                    return transactionValidationState.IsValid.Value;
                }
            }

            // Check valid inputs are greater than token outputs
            if (transactionValidationState.Transaction.Type == SlpTransactionType.SEND)
            {
                decimal validInputQty = 0m;
                transactionValidationState.Parents.ForEach(p => { 
                    if (p.IsValid == true) {
                        if (!p.InputQty.HasValue) //TODO: check this error out - is this burn tx issue
                            throw new Exception("Input quantity not set for " + p.TrHex + ":" + p.VOut);
                        validInputQty += p.InputQty.Value; 
                    } 
                });
                var tokenOutQty = slpTx.TokenOutputSum; 
                if (tokenOutQty > validInputQty)
                {
                    transactionValidationState.IsValid = false;
                    transactionValidationState.Waiting = false;
                    transactionValidationState.InvalidReason = "Token outputs are greater than valid token inputs.";
                    return transactionValidationState.IsValid.Value;
                }
            }

            // Check versionType is not different from valid parents
            if (transactionValidationState.Parents.Any(p => p.IsValid==true)) 
            {
                var validVersionType = transactionValidationState.Parents.Find(p => p.IsValid==true).VersionType;
                if (transactionValidationState.Transaction.SlpTokenType != validVersionType)
                {
                    transactionValidationState.IsValid = false;
                    transactionValidationState.Waiting = false;
                    transactionValidationState.InvalidReason = "SLP version/type mismatch from valid parent.";
                    return transactionValidationState.IsValid.Value!;
                }
            }
            transactionValidationState.IsValid = true;
            transactionValidationState.Waiting = false;
            
            return transactionValidationState.IsValid.Value;
        }

        #endregion
    }
}
