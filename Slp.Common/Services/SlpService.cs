using Slp.Common.Extensions;
using Slp.Common.Models;
using Slp.Common.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using Slp.Common.Interfaces;
using Slp.Common.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Slp.Common.DataAccess;
using Slp.Common.Models.DbModels;

namespace Slp.Common.Services
{
    public class SlpService : ISlpService
    {
        private readonly ILogger<SlpService> _log;
        private readonly RPCClient _rpcClient;
        private readonly ISlpValidator _slpValidator;
        private readonly SlpDbContext _slpDb;
        public SlpService(
            ILogger<SlpService> log, 
            RPCClient rpcClient, 
            ISlpValidator slpValidator,
            SlpDbContext slpDb
            )
        {
            _rpcClient = rpcClient;
            _log = log;
            _slpValidator = slpValidator;
            _slpDb = slpDb;
        }

        public bool IsPotentialSlpTransaction(byte[] scriptPubKeyAtIndex0)
        {
            if (scriptPubKeyAtIndex0 == null)
                return false;
            if (scriptPubKeyAtIndex0.Length < 6) //check for OP_RETURN + lokad_id SLP0 ( 4 bytes
                return false;
            if (scriptPubKeyAtIndex0[0] != (byte)SlpScript.OP_RETURN)
                return false;
            if (scriptPubKeyAtIndex0[1] != 0x04)
                return false;
            if (scriptPubKeyAtIndex0[2] != 0x53)
                return false;
            if (scriptPubKeyAtIndex0[3] != 0x4c)
                return false;
            if (scriptPubKeyAtIndex0[4] != 0x50)
                return false;
            if (scriptPubKeyAtIndex0[5] != 0x00)
                return false;
            return true;
        }
        public SlpTransaction GetSlpTransaction(NBitcoin.Transaction tr)
        {
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (!tr.Outputs.Any())
                return null;
            var outputScriptAt0 = tr.Outputs.First().ScriptPubKey.ToBytes();

            var isSlp = IsPotentialSlpTransaction(outputScriptAt0);
            if (!isSlp)
                return null;
            try
            {
                return ParseSlpTransaction(tr);
            }
            catch {
                _log.LogWarning("OP_RETURN at output 0 transaction {0} does not have valid SLP data!", tr.GetHash().ToString());
                return null; 
            }
        }
        public TransactionDetails GetTransactionDetails(byte[] scriptPubKeyAtIndex0)
        {
            if (scriptPubKeyAtIndex0 == null)
                throw new ArgumentNullException(nameof(scriptPubKeyAtIndex0));
            try
            {
                return ParseSlpOutputScript(scriptPubKeyAtIndex0);
            }
            catch (Exception)
            {
                return null;
            }
        }
        public async Task<decimal> GetTxFeeInBchAsync(string txHex)
        {
            var tx = await _rpcClient.GetRawTransactionAsync(new uint256(txHex));
            var coins =
                tx.Inputs
                .Select(i =>
                {
                    var prevTx = _rpcClient.GetRawTransaction(i.PrevOut.Hash);
                    var output = prevTx.Outputs[i.PrevOut.N];
                    return new Coin(prevTx, output);
                }).ToArray();
            var fee = tx.GetFee(coins);
            return fee.ToUnit(MoneyUnit.BTC);
        }

        public async Task<string> SendTokensManyManyAsync(string tokenHex, string collectingAddress, string[] from, string[] to, decimal[] amounts, string[] wifs)
        {
            if (!from?.Any() ?? false)
                throw new Exception("From must contain at least one address ");
            if (!to?.Any() ?? false)
                throw new Exception("TO must contain at least one address ");
            if (!wifs?.Any() ?? false)
                throw new Exception("WIFS must contain at least on item");
            if (from.Length != to.Length)
                throw new Exception("From addresses must match to addresses.");
            if (from.Length != wifs.Length)
                throw new Exception("From addresses count must match wifs count.");

            var sumToSend = amounts.Sum();
            //var slpToken = await _bchRestClient.GetTokenAsync(tokenHex);
            var slpToken = await _slpDb.SlpToken.FindAsync(tokenHex);
            var sendAmounts = amounts.Select(a => a.ToRawValue(slpToken.Decimals)).ToArray();

            var fundingAddresses = from.Select(s=>s.ToPrefixedSlpAddress()).Distinct().ToList();
            if (fundingAddresses.Count >= 19)
                throw new Exception("Funding addresses cannot exceed 19 inputs.");
            collectingAddress = collectingAddress.ToPrefixedSlpAddress();
            var fundingAddressesCombinedBalance = 0m;
            //Get token utxo
            var inputUtxos = new List<AddressUtxoResult>();
            foreach (var fundingAddress in fundingAddresses)
            {
                var balancesResults = await GetAllSlpBalancesAndUtxosAsync(fundingAddress);
                if (balancesResults.Count != 1 || !balancesResults.First().Value.SlpTokenUtxos.ContainsKey(tokenHex))
                    continue;
                inputUtxos.AddRange(balancesResults.First().Value.SlpTokenUtxos[tokenHex]);
                // fundingAddressesCombinedBalance += balancesResults.First().Value.SlpTokenBalances;
                if (fundingAddressesCombinedBalance >= sumToSend)
                    break;
            }
            if (fundingAddressesCombinedBalance < sumToSend && !fundingAddresses.Contains(collectingAddress))
                fundingAddresses.Add(collectingAddress);
            // Get BCH utxo for txtFee
            var keyBalances = await GetAllSlpBalancesAndUtxosAsync(collectingAddress);
            if (keyBalances.Count != 1 || !keyBalances.First().Value.NonSlpUtxos.Any())
                throw new Exception("No bch for txtFee");
            inputUtxos.AddRange(keyBalances.First().Value.NonSlpUtxos);
            // Add Wifs
            for (int i = 0; i < inputUtxos.Count; i++)
                inputUtxos[i].Wif = wifs[i];
            
            // set addresses that will recive tokens
            if (to.Length != sendAmounts.Count())
                throw new Exception("to.length != sendAmounts.length");
            // set where change will go
            var bchChangeReceiverAddress = collectingAddress;
            var sendTxid = await SimpleTokenSendAsync(
                tokenHex,
                sendAmounts.ToArray(),
                inputUtxos.ToArray(),
                to,
                bchChangeReceiverAddress,
                Array.Empty<NonTokenOutput>()
            );
            return sendTxid;
        }

        public async Task<string> SendTokensExactAsync(string tokenHex, string from, string to, decimal amount, string wif)
        {
            from.EnsureSlpAddress();
            to.EnsureSlpAddress();
            //var slpToken = await _bchRestClient.GetTokenAsync(tokenHex);
            var slpToken = await _slpDb.SlpToken.FindAsync(tokenHex);
            var sendAmounts = new decimal[] { amount.ToRawValue(slpToken.Decimals) };

            var fundingAddresses = new string[] { from };

            var inputUtxos = new List<AddressUtxoResult>();
            foreach (var key in fundingAddresses)
            {
                var balances = await GetAllSlpBalancesAndUtxosAsync(key);
                if (balances.Count != 1 || !balances.First().Value.SlpTokenUtxos.TryGetValue(tokenHex, out List<AddressUtxoResult> addrs))
                    continue;
                inputUtxos.AddRange(addrs);
            }
            var collectingAddress = from;
            // Get BCH utxo for txtFee
            var keyBalances = await GetAllSlpBalancesAndUtxosAsync(collectingAddress);
            if (keyBalances.Count != 1 || !keyBalances.First().Value.NonSlpUtxos.Any())
                throw new Exception("No bch for txtFee");
            inputUtxos.AddRange(keyBalances.First().Value.NonSlpUtxos);

            // Add Wifs
            inputUtxos.ForEach(txo => {
                var address = txo.CashAddress.ToAddress(_rpcClient.GetSlpPrefix());
                txo.Wif = wif;
            });

            // set addresses that will recive tokens
            var tokenReceiverAddress = new string[] { to };
            // set where change will go
            var bchChangeReceiverAddress = collectingAddress;
            var sendTxId = await SimpleTokenSendAsync(
                tokenHex,
                sendAmounts,
                inputUtxos.ToArray(),
                tokenReceiverAddress,
                bchChangeReceiverAddress,
                Array.Empty<NonTokenOutput>()
            );
            return sendTxId;
        }


        /// <summary>
        /// Prepares transaction data without sending it to network.
        /// </summary>
        /// <param name="tokenIdHex"></param>
        /// <param name="sendAmounts"></param>
        /// <param name="inputUtxos"></param>
        /// <param name="tokenReceiverAddresses"></param>
        /// <param name="tokenChangeReceiverAddress"></param>
        /// <param name="bchChangeReceiverAddress"></param>
        /// <param name="requiredNonTokenOutputs"></param>
        /// <returns></returns>
        public Transaction PrepareTokenSendTransaction(
            string tokenIdHex, 
            decimal[] sendAmounts, 
            AddressUtxoResult[] inputUtxos,
            string[] tokenReceiverAddresses, 
            string tokenChangeReceiverAddress, 
            string bchChangeReceiverAddress,
            NonTokenOutput[] requiredNonTokenOutputs
            )
        {
            return TransactionHelpers.SimpleTokenSend(
              this,
              tokenIdHex,
              sendAmounts,
              inputUtxos,
              tokenReceiverAddresses,
              tokenChangeReceiverAddress,
              bchChangeReceiverAddress,
              requiredNonTokenOutputs,
              0);
        }

        public async Task<NBitcoin.Transaction> SimpleTokenSendAsync2(string tokenIdHex, decimal[] sendAmounts, AddressUtxoResult[] inputUtxos,
           string[] tokenReceiverAddresses, string tokenChangeReceiverAddress, string bchChangeReceiverAddress,
           NonTokenOutput[] requiredNonTokenOutputs
           )
        {
            var tx = TransactionHelpers.SimpleTokenSend(
               this,
               tokenIdHex,
               sendAmounts,
               inputUtxos,
               tokenReceiverAddresses,
               tokenChangeReceiverAddress,
               bchChangeReceiverAddress,
               requiredNonTokenOutputs,
               0);
            if (tx == null)
                throw new Exception("Failed to prepare simple token send for token {tokenIdHex}");
            await SendTransactionAsync(tx);
            return tx;
        }

      
        /// <summary>
        /// Send token amoutns to different outputs
        /// </summary>
        /// <param name="tokenIdHex"></param>
        /// <param name="sendAmounts"></param>
        /// <param name="inputUtxos"></param>
        /// <param name="tokenReceiverAddresses"></param>
        /// <param name="changeReceiverAddress"></param>
        /// <param name="requiredNonTokenOutputs"></param>
        /// <returns></returns>
        public async Task<string> SimpleTokenSendAsync(string tokenIdHex, decimal[] sendAmounts, AddressUtxoResult[] inputUtxos,
            string[] tokenReceiverAddresses, string changeReceiverAddress,
            NonTokenOutput[] requiredNonTokenOutputs
            )
        {
            var tx = TransactionHelpers.SimpleTokenSend(
                this, 
                tokenIdHex, 
                sendAmounts, 
                inputUtxos,
                tokenReceiverAddresses, 
                changeReceiverAddress, 
                null,
                requiredNonTokenOutputs,0);
            if (tx == null)
                throw new Exception("Failed to prepare simple token send for token {tokenIdHex}");
            await SendTransactionAsync(tx);
            return tx.GetHash().ToString();
        }
        public async Task<string> SendTransactionAsync(Transaction tx)
        {
            var res = await _rpcClient.SendRawTransactionAsync(tx);
            if (res == null)
                throw new Exception($"Sending raw transaction failed for {tx.GetHash()}");
            return res.ToString();
        }

        public async Task<Dictionary<string, BalancesResult>> GetAllSlpBalancesAndUtxosAsync(params string[] addresses)
        {
            var res = new Dictionary<string, BalancesResult>();
            foreach (var address in addresses)
            {
                //var bchAddress = address.ToAddress(_rpcClient.GetBchPrefix());
                var utxos = await GetUtxoWithTxDetailsAsync(address);
                var result = await ProcessUtxosForSlpAsync(utxos);
                res.Add(address.ToAddress(_rpcClient.GetSlpPrefix()), result);
            }
            return res;
        }
        private async Task<BalancesResult>  ProcessUtxosForSlpAsync(AddressUtxoResult[] utxos)
        {
            // 1) parse SLP OP_RETURN and cast initial SLP judgement, based on OP_RETURN only.
            foreach(var txo in utxos) 
            {
                // first check if the .tx property is being used (this is how BITBOX returns results via REST API)
                var tx = txo.Tx;
                var slpMsgBuf = tx.Outputs.First().ScriptPubKey.ToBytes();
                ApplyInitialSlpJudgement(txo, slpMsgBuf);
                if ( txo.SlpUtxoJudgement == SlpUtxoJudgement.UNKNOWN ) 
                    throw new Exception ("Utxo SLP judgement has not been set, unknown error.");

            }
            //// 2) Cast final SLP judgement using the supplied async validator
            await this.ApplyFinalSlpJudgementAsync(utxos);

            // 3) Prepare results object
            var result = this.ComputeSlpBalances(utxos);

            // 4) Check that all UTXOs have been categorized
            var tokenTxoCount = result.SlpTokenUtxos.Sum(t => t.Value.Count);
            //foreach (var id in result.SlpTokenUtxos) {
            //    tokenTxoCount += id.Value.Count;
            //}
            var batonTxoCount = result.SlpBatonUtxos.Sum(b => b.Value.Count);
            //        for (const id in result.slpBatonUtxos) {
            //    batonTxoCount += result.slpBatonUtxos[id].length;
            //}
            if (utxos.Length != (tokenTxoCount + batonTxoCount + result.NonSlpUtxos.Count 
                + result.UnknownTokenTypeUtxos.Count + result.InvalidBatonUtxos.Count + result.InvalidTokenUtxos.Count) )
                throw new Exception("Not all UTXOs have been categorized. Unknown Error.");
            //            result.unknownTokenTypeUtxos.length + result.invalidBatonUtxos.length + result.invalidTokenUtxos.length)) {
            //    throw Error("Not all UTXOs have been categorized. Unknown Error.");
            //}
            return result;
        }

        private BalancesResult ComputeSlpBalances(AddressUtxoResult[] utxos)
        {
            var result = new BalancesResult();
            // 5) Loop through UTXO set and accumulate balances for type of utxo, organize the Utxos into their categories.
            foreach (var txo in utxos) 
            {
                if (txo.Satoshis==null && txo.Value.HasValue)
                    txo.Satoshis = Math.Floor(txo.Value.Value * (decimal)(10^8));
                else if (!txo.Satoshis.HasValue)
                    throw new Exception("Txo is missing 'satoshis' and 'value' property");
                
                if (txo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_TOKEN)
                {
                    if (!result.SlpTokenBalances.ContainsKey(txo.TransactionDetails.TokenIdHex) ) 
                        result.SlpTokenBalances.Add(txo.TransactionDetails.TokenIdHex,0m);
                    
                    if (txo.TransactionDetails.TransactionType == SlpTransactionType.GENESIS ||
                        txo.TransactionDetails.TransactionType == SlpTransactionType.MINT)
                    {
                        result.SlpTokenBalances[txo.TransactionDetails.TokenIdHex] =
                            result.SlpTokenBalances[txo.TransactionDetails.TokenIdHex] +
                            txo.TransactionDetails.GenesisOrMintQuantity.Value;
                    }
                    else if (txo.TransactionDetails.TransactionType == SlpTransactionType.SEND &&
                      txo.TransactionDetails.SendOutputs != null)
                    {
                        var qty = txo.TransactionDetails.SendOutputs[txo.VOut];
                        result.SlpTokenBalances[txo.TransactionDetails.TokenIdHex] =
                            result.SlpTokenBalances[txo.TransactionDetails.TokenIdHex] + qty;
                    }
                    else
                    {
                        throw new Exception("Unknown Error: cannot have an SLP_TOKEN that is not from GENESIS, MINT, or SEND.");
                    }
                    result.SatoshisInSlpToken += txo.Satoshis.Value;

                    if (!result.SlpTokenUtxos.ContainsKey(txo.TransactionDetails.TokenIdHex))
                        result.SlpTokenUtxos.Add(txo.TransactionDetails.TokenIdHex, new List<AddressUtxoResult>());                    
                    // NFT1 Children Balances (nftParentChildBalances):
                    if (txo.TransactionDetails.VersionType == SlpVersionType.TokenVersionType1_NFT_Child)
                    {
                        if (!result.NftParentChildBalances.ContainsKey(txo.NftParentId))
                            result.NftParentChildBalances.Add(txo.NftParentId, new Dictionary<string, decimal>());

                        if (!result.NftParentChildBalances[txo.NftParentId].ContainsKey(txo.TransactionDetails.TokenIdHex))
                            result.NftParentChildBalances[txo.NftParentId].Add(txo.TransactionDetails.TokenIdHex, txo.SlpUtxoJudgementAmount.Value);                            
                        else
                        {
                            // NOTE: this does not cover the 0 quantity SEND case
                            throw new Exception("Cannot have 2 UTXOs with the same NFT1 child token designation.");
                        }
                    }
                    // All token balances (includes Type 1, and NFT1(65/129)):
                    result.SlpTokenUtxos[txo.TransactionDetails.TokenIdHex].Add(txo);
                }
                else if (txo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_BATON)
                {
                    result.SatoshisInSlpBaton += txo.Satoshis.Value;
                    if (!result.SlpBatonUtxos.ContainsKey(txo.TransactionDetails.TokenIdHex))
                        result.SlpBatonUtxos.Add(txo.TransactionDetails.TokenIdHex, new List<AddressUtxoResult>());                    
                    result.SlpBatonUtxos[txo.TransactionDetails.TokenIdHex].Add(txo);
                }
                else if (txo.SlpUtxoJudgement == SlpUtxoJudgement.INVALID_TOKEN_DAG)
                {
                    result.SatoshisInInvalidTokenDAG += txo.Satoshis.Value;
                    result.InvalidTokenUtxos.Add(txo);
                }
                else if (txo.SlpUtxoJudgement == SlpUtxoJudgement.INVALID_BATON_DAG)
                {
                    result.SatoshisInInvalidBatonDag += txo.Satoshis.Value;
                    result.InvalidBatonUtxos.Add(txo);
                }
                else if (txo.SlpUtxoJudgement == SlpUtxoJudgement.UNSUPPORTED_TYPE)
                {
                    result.SatoshisInUnknownTokenType += txo.Satoshis.Value;
                    result.UnknownTokenTypeUtxos.Add(txo);
                }
                else
                {
                    result.SatoshisAvailableBch += txo.Satoshis.Value;
                    result.NonSlpUtxos.Add(txo);
                }
            }
            return result;
        }

        public void ApplyInitialSlpJudgement(AddressUtxoResult txo, byte[] slpMsgBuf)
        {
            try
            {
                txo.TransactionDetails = this.ParseSlpOutputScript(slpMsgBuf);
                // populate txid for GENESIS
                if (txo.TransactionDetails.TransactionType == SlpTransactionType.GENESIS)
                    txo.TransactionDetails.TokenIdHex = txo.TxId;
                
                // apply initial SLP judgement to the UTXO (based on OP_RETURN
                // parsing ONLY! Still need to validate the DAG for possible tokens and batons!)
                if (txo.TransactionDetails.TransactionType == SlpTransactionType.GENESIS ||
                    txo.TransactionDetails.TransactionType == SlpTransactionType.MINT)
                {
                    if (txo.TransactionDetails.ContainsBaton && txo.TransactionDetails.BatonVOut == txo.VOut)
                        txo.SlpUtxoJudgement = SlpUtxoJudgement.SLP_BATON;
                    else if (txo.VOut == 1 && txo.TransactionDetails.GenesisOrMintQuantity > 0)
                    {
                        txo.SlpUtxoJudgement = SlpUtxoJudgement.SLP_TOKEN;
                        txo.SlpUtxoJudgementAmount = txo.TransactionDetails.GenesisOrMintQuantity;
                    }
                    else
                        txo.SlpUtxoJudgement = SlpUtxoJudgement.NOT_SLP;
                    
                }
                else if (txo.TransactionDetails.TransactionType == SlpTransactionType.SEND &&
                  txo.TransactionDetails.SendOutputs != null)
                {
                    if (txo.VOut > 0 && txo.VOut < txo.TransactionDetails.SendOutputs.Count)
                    {
                        txo.SlpUtxoJudgement = SlpUtxoJudgement.SLP_TOKEN;
                        txo.SlpUtxoJudgementAmount = txo.TransactionDetails.SendOutputs[txo.VOut];
                    }
                    else
                        txo.SlpUtxoJudgement = SlpUtxoJudgement.NOT_SLP;                    
                }
                else
                    txo.SlpUtxoJudgement = SlpUtxoJudgement.NOT_SLP;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Unsupported token type"))
                    txo.SlpUtxoJudgement = SlpUtxoJudgement.UNSUPPORTED_TYPE;
                else
                    txo.SlpUtxoJudgement = SlpUtxoJudgement.NOT_SLP;
            }
        }

        private async Task ApplyFinalSlpJudgementAsync(AddressUtxoResult[] utxos)
        {
            //we do not need slp validate here - sync already performs validation for each tx and saves state
            var slpTrToCheck = utxos.Where(u =>
                 u.TransactionDetails != null &&
                 u.SlpUtxoJudgement != SlpUtxoJudgement.UNKNOWN &&
                 u.SlpUtxoJudgement != SlpUtxoJudgement.UNSUPPORTED_TYPE &&
                 u.SlpUtxoJudgement != SlpUtxoJudgement.NOT_SLP).Select(u => u.TxId).ToArray();
            //we have our slp db to check for validity no need to to traverse through graph
            //var validSlpTxs = await _slpDb.SlpTransaction.Where(t => slpTrToCheck.Contains(t.Hex) && t.State == SlpTransaction.TransactionState.SLP_VALID).Select(s=>s.Hex).ToArrayAsync();
            var txs = utxos.Select(s => s.TxId).ToArray();
            //var slpToken = await _slpDb.SlpToken.FindAsync(tokenHex);
            //var txsSlpState = await _bchRestClient.ValidateSlpTransactionsAsync(txs);
            var validSlpTxs = await _slpValidator.ValidateSlpTransactionsAsync(txs);
            foreach (var utxo in utxos)
            {
                if (!validSlpTxs.Contains(utxo.TxId))
                //if( !txsSlpState.Any(t => t.txid == utxo.TxId && t.valid) )
                {
                    if (utxo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_TOKEN)
                        utxo.SlpUtxoJudgement = SlpUtxoJudgement.INVALID_TOKEN_DAG;
                    else if (utxo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_BATON)
                        utxo.SlpUtxoJudgement = SlpUtxoJudgement.INVALID_BATON_DAG;
                }
            }
            // Loop through utxos to add nftParentId to any NFT1 child UTXO.
            foreach(var txo in utxos) 
            {
                if (txo.TransactionDetails!=null &&
                    txo.TransactionDetails.VersionType == SlpVersionType.TokenVersionType1_NFT_Child)
                {
                    if (txo.TransactionDetails.TransactionType == SlpTransactionType.GENESIS)
                        txo.NftParentId = await GetNftParentIdAsync(txo.TransactionDetails.TokenIdHex);
                    else
                        txo.NftParentId = await GetNftParentIdAsync(txo.TxId);                    
                }
            }
        }

        private async Task<string> GetNftParentIdAsync(string tokenIdHex)
        {
            var slpTx = await _slpValidator.GetTransactionAsync(tokenIdHex);
            //var nftBurnTxnHex = await _slpValidator.getRawTransactions([tx.inputs[0].previousTxHash]))[0];
            var nftBurnSlpTx = await _slpValidator.GetTransactionAsync(slpTx.SlpTransactionInputs.First().SourceTxHash.ToHex());
            // const slp = new Slp(this.BITBOX);
            //const nftBurnSlp = slp.parseSlpOutputScript(Buffer.from(nftBurnTxn.outputs[0].scriptPubKey));
            if (nftBurnSlpTx.Type == SlpTransactionType.GENESIS) 
                return slpTx.SlpTransactionInputs.First().SourceTxHash.ToHex();
            else 
                return nftBurnSlpTx.SlpTokenId.ToHex();
        }

        private async Task<AddressUtxos> GetUtxoWithRetryAsync(string address, int retries = 40)
        {
            int retryCount = 0;
            while (retryCount++ < 40)
            {
                try
                {
                    if (address.IsSlpAddress())
                    {
                        var result = new AddressUtxos();
                        var utxos = await _slpDb.SlpTransactionOutput
                            .Include(o => o.SlpTransaction)
                            .Include(o => o.Address)
                            .Where(o => o.NextInputId == null && o.Address.Address == address)
                            .Select(s => new AddressUtxo()
                            {
                                Amount = s.Amount,
                                Height = s.SlpTransaction.BlockHeight.Value,
                                Confirmations = -1,
                                Satoshis = s.BlockchainSatoshis,
                                TxId = s.SlpTransaction.Hash.ToHex(),
                                VOut = s.VOut
                            }).ToListAsync();


                        var prefix = _rpcClient.Network.ChainName.GetBchPrefix();
                        return new AddressUtxos()
                        {
                            Utxos = utxos.ToArray(),
                            SlpAddress = address,
                            CashAddress = address.ToAddress(prefix),
                            LegacyAddress = null
                        };
                    }
                    else //we need to fetch from bch indexer
                    {
                        var prefix = _rpcClient.Network.ChainName.GetBchPrefix();
                        var bchAddress = address.ToAddress(prefix);

                        throw new NotImplementedException(
                            "Missing feature: SlpCs need bch indexer connection to fetch utxo-s for current bch address");
                        //return await _bchRestClient.GetAddressUtxosAsync(bchAddress);
                    }
                }
                catch (Exception)
                {
                }
            }
            throw new Exception("Failed to execute Utxo after 40 retries");
        }

        private async Task<List<NBitcoin.Transaction>> GetTransactionsAsync(string[] txids)
        {
            //TODO: use sql db to retrieve data faster and then only check live node for any missing transactions
            var results = new ConcurrentBag<NBitcoin.Transaction>();
            var tasks = new List<Task>();
            foreach (var txid in txids)
                tasks.Add(
                    Task.Run(() => {
                        var tx = _rpcClient.GetRawTransaction(new uint256(txid));
                        results.Add(tx);
                    }));
            await Task.WhenAll(tasks);
            return results.ToList();
        }


        public async Task<AddressUtxoResult[]> GetUtxoWithTxDetailsAsync(string address)
        {
            var res = await GetUtxoWithRetryAsync(address);
            var slpUtxos = res.Utxos.Select(o => new AddressUtxoResult
            {
                Satoshis = o.Satoshis,
                Confirmations = o.Confirmations,
                CashAddress = res.CashAddress,
                TxId = o.TxId,
                VOut = o.VOut,
                Height = o.Height,
                Amount = o.Amount,
                //LegacyAddress =  res.LegacyAddress,
                //ScriptPubKey = res.ScriptPubKey,
            }).ToArray(); 
            var txIds = slpUtxos.Select(u => u.TxId).ToArray();
            if (!txIds.Any())
                return new AddressUtxoResult[] { };

            var txs = await GetTransactionsAsync(txIds.ToArray());

            foreach (var utxo in slpUtxos)
            {
                var tx = txs.Find(t => t.GetHash().ToString() == utxo.TxId);
                utxo.Tx = tx;
            }
            // Split txIds into chunks of 20 (BitBox limit), run the detail queries in parallel
            // Parallel.ForEach()
            //let txDetails: any[] = (await Promise.all(_.chunk(txIds, 20).map((txids: any[]) => {
            //    return this.getTransactionDetailsWithRetry([...new Set(txids)]);
            //})));
            // concat the chunked arrays
            // txDetails = ([].concat(...txDetails) as TxnDetailsResult[]);
            // utxos = utxos.map((i: SlpAddressUtxoResult) => { i.tx = txDetails.find((d: TxnDetailsResult) => d.txid === i.txid); return i; });
            return slpUtxos;
        }


        public NBitcoin.Transaction BuildRawSendTx(ConfigBuildRawSendTx config, SlpVersionType type = SlpVersionType.TokenVersionType1)
        {
            // Check proper address formats are given
            if( !config.TokenReceiverAddressArray.All(a => a.IsSlpAddress()) )
                throw new Exception("Not all token receiver address in SlpAddr format.");
            if (!config.TokenChangeReceiverAddress.IsSlpAddress())
                throw new Exception("Token change receiver address is not in SLP format.");
            if (config.BchChangeReceiverAddress.IsSlpAddress())
                throw new Exception("BCH change receiver address is SLP format.");
            
            // Parse the SLP SEND OP_RETURN message
            var sendMsg = this.ParseSlpOutputScript(config.SlpSendOpReturn);

            // Make sure we're not spending inputs from any other token or baton

            var tokenInputQty = 0m;
            foreach (var txo in config.InputTokenUtxos)
            {
                if (txo.SlpUtxoJudgement == SlpUtxoJudgement.NOT_SLP)
                    continue;
                if (txo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_TOKEN)
                {
                    if (txo.TransactionDetails.TokenIdHex != sendMsg.TokenIdHex)
                        throw new Exception("Input UTXOs included a token for another tokenId.");
                    tokenInputQty = tokenInputQty + txo.SlpUtxoJudgementAmount;
                    continue;
                }
                if (txo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_BATON)
                    throw new Exception("Cannot spend a minting baton.");
                if (txo.SlpUtxoJudgement == SlpUtxoJudgement.INVALID_TOKEN_DAG ||
                    txo.SlpUtxoJudgement == SlpUtxoJudgement.INVALID_BATON_DAG)
                    throw new Exception("Cannot currently spend UTXOs with invalid DAGs.");
                throw new Exception("Cannot spend utxo with no SLP judgement.");
            }
            // Make sure the number of output receivers
            // matches the outputs in the OP_RETURN message.
            var chgAddr = config.BchChangeReceiverAddress != null ? 1 : 0;
            if (sendMsg.SendOutputs == null)
                throw new Exception("OP_RETURN contains no SLP send outputs.");
            if (config.TokenReceiverAddressArray.Length + chgAddr != sendMsg.SendOutputs.Count)
                throw new Exception("Number of token receivers in config does not match the OP_RETURN outputs");
            // Make sure token inputs == token outputs
            var outputTokenQty = sendMsg.SendOutputs.Sum(); // reduce((v, o) => v = v.plus(o), new BigNumber(0));
            if (tokenInputQty != outputTokenQty)
                throw new Exception("Token input quantity does not match token outputs.");

            // Create a transaction builder
            //const transactionBuilder = new this.BITBOX.TransactionBuilder(
            //    Utils.txnBuilderString(config.tokenReceiverAddressArray[0]));
            var receiverAddress = config.TokenReceiverAddressArray[0];
            //var transactionBuilder = _rpcClient.Network.CreateTransactionBuilder();
            var transaction = _rpcClient.Network.CreateTransaction();

            // Calculate the total input amount & add all inputs to the transaction
            var inputSatoshis = config.InputTokenUtxos.Sum(i => i.Satoshis); //.reduce((t, i) => t.plus(i.satoshis), new BigNumber(0));
            foreach (var txo in config.InputTokenUtxos)
            {
                var txIn = new TxIn(new OutPoint(new uint256(txo.TxId), txo.VOut));
                transaction.Inputs.Add(txIn);
            }
            // Calculate the amount of outputs set aside for special BCH-only outputs for fee calculation
            var bchOnlyCount = config.RequiredNonTokenOutputs?.Length ?? 0;
            var bcOnlyOutputSatoshis = config.RequiredNonTokenOutputs?.Sum(t => t.Satoshis) ?? 0; // reduce((t, v) => t += v.satoshis, 0) : 0;

            // Calculate mining fee cost
            var sendCost = this.CalculateSendCost(
                                config.SlpSendOpReturn.Length,
                                config.InputTokenUtxos.Length,
                                config.TokenReceiverAddressArray.Length + bchOnlyCount,
                                config.BchChangeReceiverAddress)
                                +
                                (config.ExtraFee ?? 0);

            // Compute BCH change amount
            var bchChangeAfterFeeSatoshis = inputSatoshis - sendCost - bcOnlyOutputSatoshis;
            // Start adding outputs to transaction
            // Add SLP SEND OP_RETURN message
            //transactionBuilder.addOutput(config.slpSendOpReturn, 0);
            transaction.Outputs.Add(Money.Satoshis(0), new Script(config.SlpSendOpReturn) );
            // Add dust outputs associated with tokens
            foreach(var outputAddress in config.TokenReceiverAddressArray)
            {
                //var addressData = outputAddress.DecodeBCashAddress(); // (_rpcClient.Network.GetBase58CheckEncoder()); // (_rpcClient.Network.NetworkType.GetBchPrefix());
                var addressData = outputAddress.DecodeSlpAddress();
                //var cashAddress = outputAddress.ToAddress(_rpcClient.GetBchPrefix());
                var key = new KeyId(addressData.Hash);
                var bchAddress = _rpcClient.Network.CreateBitcoinAddress(key);
                var txOut = new TxOut(Money.Satoshis(SD.SLPTransactionBchDustAmmount546),bchAddress.ScriptPubKey);
                transaction.Outputs.Add(txOut);
            }           
            // Add BCH-only outputs
            if (config.RequiredNonTokenOutputs!=null && config.RequiredNonTokenOutputs.Length > 0)
            {
                foreach (var nto in config.RequiredNonTokenOutputs)
                {
                    //var bchAddressstring = nto.ReceiverAddress.ToAddress(_rpcClient.GetBchPrefix());
                    var addressData = nto.ReceiverAddress.DecodeBCashAddress();
                    var key = new KeyId(addressData.Hash);
                    var bchAddress = _rpcClient.Network.CreateBitcoinAddress(key);
                    transaction.Outputs.Add(Money.Satoshis(nto.Satoshis), bchAddress.ScriptPubKey);
                }
            }
            // Add change, if any
            if (bchChangeAfterFeeSatoshis > SD.SLPTransactionBchDustAmmount546)
            {
                //config.bchChangeReceiverAddress = bchaddr.toCashAddress(config.bchChangeReceiverAddress);
                config.BchChangeReceiverAddress = config.BchChangeReceiverAddress.ToAddress(_rpcClient.GetBchPrefix());
                var addressData = config.BchChangeReceiverAddress.DecodeBCashAddress();
                var key = new KeyId(addressData.Hash);
                var bchAddress = _rpcClient.Network.CreateBitcoinAddress(key);
                transaction.Outputs.Add(Money.Satoshis(bchChangeAfterFeeSatoshis), bchAddress.ScriptPubKey);
            }
            // Sign txn and add sig to p2pkh input for convenience if wif is provided,
            // otherwise skip signing.

            //uint i = 0;
            var keys = new List<BitcoinSecret>();
            var coins = new List<ICoin>();
            foreach (var input in transaction.Inputs)
            {
                var dataInput = config.InputTokenUtxos.FirstOrDefault(t => t.TxId == input.PrevOut.Hash.ToString() && t.VOut == input.PrevOut.N);
                if (dataInput?.Wif == null)
                    continue;
                var privKey = Key.Parse(dataInput.Wif, _rpcClient.Network);
                var secret = privKey.GetBitcoinSecret(_rpcClient.Network);
                keys.Add(secret);

                //how to get ICoin from input
                var prevTx = _rpcClient.GetRawTransaction(input.PrevOut.Hash);
                var output = prevTx.Outputs[input.PrevOut.N];
                var coin = new Coin(prevTx, output);
                coins.Add(coin);
            }
            transaction.Sign(keys, coins);
            var txasStr = transaction.ToString();
            var hex = transaction.GetHash().ToString();
            // Check For Low Fee
            var outValue = transaction.Outputs.Sum(o => o.Value); // .reduce((v: number, o: any) => v += o.value, 0);
            var inValue = config.InputTokenUtxos.Sum(i => i.Satoshis); //.reduce((v, i) => v = v.plus(i.satoshis), new BigNumber(0));
            if (inValue - outValue <= hex.Length / 2 )
                throw new Exception("Transaction input BCH amount is too low.  Add more BCH inputs to fund this transaction.");
            return transaction;
        }

        public string BuildRawGenesisTx(ConfigBuildRawGenesisTx config, SlpVersionType slpVersionType)
        {

            if (config.MintReceiverSatoshis == null)
            {
                config.MintReceiverSatoshis = SD.SLPTransactionBchDustAmmount546;
            }

            if (config.BatonReceiverSatoshis == null)
            {
                config.BatonReceiverSatoshis = SD.SLPTransactionBchDustAmmount546;
            }

            // Make sure we're not spending any token or baton UTXOs
            foreach (var txo in config.InputUtxos)
            {
                if (txo.SlpUtxoJudgement == SlpUtxoJudgement.NOT_SLP)
                    return null;
                if (config.AllowedTokenBurning!=null &&
                    txo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_TOKEN &&
                    config.AllowedTokenBurning.Contains(txo.TransactionDetails.TokenIdHex))
                    return null; 
                else if (txo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_TOKEN)
                    throw new Exception("Input UTXOs included a token for another tokenId.");
                if (txo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_BATON)
                    throw new Exception("Cannot spend a minting baton.");
                if (txo.SlpUtxoJudgement == SlpUtxoJudgement.INVALID_TOKEN_DAG ||
                    txo.SlpUtxoJudgement == SlpUtxoJudgement.INVALID_BATON_DAG)
                    throw new Exception("Cannot currently spend tokens and baton with invalid DAGs.");
                throw new Exception("Cannot spend utxo with no SLP judgement.");
            }
            // Check for slp formatted addresses
            if(config.MintReceiverAddress.IsSlpAddress())
                throw new Exception("Not an SLP address.");
            if (! (config.BatonReceiverAddress?.IsSlpAddress() ?? false ))
                throw new Exception("Not an SLP address.");

            config.MintReceiverAddress = config.MintReceiverAddress.ToAddress(_rpcClient.GetBchPrefix()); //bchaddr.toCashAddress(config.mintReceiverAddress);

            var transaction = _rpcClient.Network.CreateTransaction();
            var transactionBuilder = _rpcClient.Network.CreateTransactionBuilder();
            //const transactionBuilder = new this.BITBOX.TransactionBuilder(
            //    Utils.txnBuilderString(config.mintReceiverAddress));
            decimal satoshis = 0m;
            foreach (var txo in config.InputUtxos)
            {
                //txo.TxId, txo.VOut
                transaction.Inputs.Add(new OutPoint(new uint256(txo.TxId), txo.VOut));
                satoshis += txo.Satoshis;
            }
            //config.input_utxos.forEach(token_utxo => {
            //    transactionBuilder.addInput(token_utxo.txid, token_utxo.vout);
            //    satoshis = satoshis.plus(token_utxo.satoshis);
            //});
            var genesisCost = CalculateGenesisCost(
                config.SlpGenesisOpReturn.Length,
                config.InputUtxos.Length,
                config.BatonReceiverAddress,
                config.BchChangeReceiverAddress) +
                (config.MintReceiverSatoshis > SD.SLPTransactionBchDustAmmount546 ? 
                config.MintReceiverSatoshis.Value - SD.SLPTransactionBchDustAmmount546 : 0) +
                (config.BatonReceiverSatoshis > SD.SLPTransactionBchDustAmmount546 ? 
                config.BatonReceiverSatoshis.Value - SD.SLPTransactionBchDustAmmount546 : 0);

            var bchChangeAfterFeeSatoshis = satoshis - genesisCost;

            // Genesis OpReturn
            transaction.Outputs.Add(Money.Satoshis(0), new Script(config.SlpGenesisOpReturn));

            var address = _rpcClient.Network.CreateBitcoinAddress(new KeyId(config.MintReceiverAddress));
            // Genesis token mint
            transaction.Outputs.Add(
                new TxOut(
                    Money.Satoshis(config.MintReceiverSatoshis.Value),
                    address)
             );
            // bchChangeAfterFeeSatoshis -= config.mintReceiverSatoshis;

            // Baton address (optional)
            var batonvout = ParseSlpOutputScript(config.SlpGenesisOpReturn).BatonVOut;
            if (config.BatonReceiverAddress != null)
            {
                config.BatonReceiverAddress = config.BatonReceiverAddress.ToAddress(_rpcClient.GetBchPrefix());
                if (batonvout != 2)
                {
                    throw new Exception("batonVout in transaction does not match OP_RETURN data.");
                }
                var batonReceiveraddress = _rpcClient.Network.CreateBitcoinAddress(new KeyId(config.BatonReceiverAddress));                
                transaction.Outputs.Add(Money.Satoshis(config.BatonReceiverSatoshis.Value), batonReceiveraddress);
                // bchChangeAfterFeeSatoshis -= config.batonReceiverSatoshis;
            }
            else
            {
                // Make sure that batonVout is set to null
                if (batonvout.HasValue)
                {
                    throw new Exception("OP_RETURN has batonVout set to vout=" + batonvout + ", but a baton receiver address was not provided.");
                }
            }

            // Change (optional)
            if (config.BchChangeReceiverAddress != null && bchChangeAfterFeeSatoshis > SD.SLPTransactionBchDustAmmount546)
            {
                config.BchChangeReceiverAddress = config.BchChangeReceiverAddress.ToAddress(_rpcClient.GetBchPrefix());
                var bchChangeReceiverAddress = _rpcClient.Network.CreateBitcoinAddress(new KeyId(config.BchChangeReceiverAddress));
                transaction.Outputs.Add(Money.Satoshis(bchChangeAfterFeeSatoshis), bchChangeReceiverAddress);
            }

            // sign inputs
            throw new NotImplementedException("Sign properly here.");
            //uint i = 0;
            //foreach (var txo in config.InputUtxos) {

            //    //var bchChangeReceiverAddress = _rpcClient.Network.wif
            //    //var secret = GetWif(Network.RegTest);
            //    //const paymentKeyPair =  this.BITBOX.ECPair.fromWIF(txo.wif);
            //    // var key = new Key() {  }
            //    //var res  = transactionBuilder.TrySignInput(transaction, i, SigHash.All, out TransactionSignature signature);
            //    varr res = transactionBuilder.Sign(transaction, i, SigHash.All, out TransactionSignature signature);
            //    //transaction.SignInput();
            //    //transactionBuilder.Sign(i, paymentKeyPair, undefined,
            //    //    transactionBuilder.hashTypes.SIGHASH_ALL, txo.satoshis.toNumber());
            //    i++;
            //}

            //var tx = transaction.GetHash().ToString();

            //// Check For Low Fee
            ////var outValue = transactionBuilder.Outputs(transaction.tx.outs.reduce((v: number, o: any) => v += o.value, 0);
            //var outValue = transaction.Outputs.Sum(o => o.Value); // (transaction.tx.outs.reduce((v: number, o: any) => v += o.value, 0);
            //var inValue = config.InputUtxos.Sum(i => i.Satoshis); // reduce((v, i) => v = v.plus(i.satoshis), new BigNumber(0));
            //if (inValue - outValue <= tx.Length / 2)
            //    throw new Exception("Transaction input BCH amount is too low.  Add more BCH inputs to fund this transaction.");
            //// TODO: Check for fee too large or send leftover to target address
            //return tx;
        }

        #region PRIVATE

        private decimal CalculateGenesisCost(int genesisOpReturnLength, int inputUtxoSize,
                                string batonAddress, string bchChangeAddress, int feeRate = 1)
        {
            return CalculateMintOrGenesisCost(genesisOpReturnLength,
                inputUtxoSize, batonAddress, bchChangeAddress, feeRate);
        }

        public decimal calculateMintCost(int mintOpReturnLength, int inputUtxoSize,
                                 string batonAddress, string bchChangeAddress, int feeRate = 1)
        {
            return CalculateMintOrGenesisCost(mintOpReturnLength, inputUtxoSize,
                batonAddress, bchChangeAddress, feeRate);
        }

        public decimal CalculateMintOrGenesisCost(int mintOpReturnLength, int inputUtxoSize,
                                          string batonAddress, string bchChangeAddress, int feeRate = 1)
        {
            var outputs = 1;
            var nonfeeoutputs = SD.SLPTransactionBchDustAmmount546;
            if (!string.IsNullOrEmpty(batonAddress))
            {
                nonfeeoutputs += SD.SLPTransactionBchDustAmmount546;
                outputs += 1;
            }
            if (!string.IsNullOrEmpty(bchChangeAddress))
                outputs += 1;

            // _rpcClient.fee
            throw new NotImplementedException();
            //var fee = this.BITBOX.BitcoinCash.getByteCount({ P2PKH: inputUtxoSize }, { P2PKH: outputs });

            //fee += mintOpReturnLength;
            //fee += 10; // added to account for OP_RETURN ammount of 0000000000000000
            //fee *= feeRate;
            //// console.log("MINT/GENESIS cost before outputs: " + fee.toString());
            //fee += nonfeeoutputs;
            //// console.log("MINT/GENESIS cost after outputs are added: " + fee.toString());
            //return fee;
        }

        //from bitbox-sdk

        struct InOutType
        {
            public string Type;
            public int Size;
        }

        private int GetByteCount(InOutType[] inputs, InOutType[] outputs) 
        {
            // from https://github.com/bitcoinjs/bitcoinjs-lib/issues/921#issuecomment-354394004
            var totalWeight = 0;
            var hasWitness = false;
            // assumes compressed pubkeys in all cases.
            var inputTypes = new Dictionary<string, int>()
            {
                { "MULTISIG-P2SH", 49*4 },
                { "MULTISIG-P2WSH", 6 + 41*4 },
                { "MULTISIG-P2SH-P2WSH", 6 + 76*4 },
                { "P2PKH", 148*4 },
                { "P2WPKH", 108 + 41 * 4 },
                { "P2SH-P2WPKH", 108 + 64 * 4 },
            };
            var outputTypes = new Dictionary<string, int>()
            {
                { "P2SH", 32*4 },
                { "P2PKH", 34*4 },
                { "P2WPKH", 31*4 },
                { "P2WSH", 43*4 }
            };

            foreach (var input in inputs)
            {
                if (input.Type.StartsWith("MULTISIG"))
                {
                    var keyParts = input.Type.Split(":");
                    if (keyParts.Length != 2)
                        throw new Exception($"Invalid input: {input.Type}");
                    var newKey = keyParts.First();
                    var mAndN = keyParts[1].Split("-").Select(s => int.Parse(s)).ToArray();
                    totalWeight += inputTypes[newKey] * input.Size;
                    var multiplier = newKey == "MULTISIG-P2SH" ? 4 : 1;
                    totalWeight += (73 * mAndN[0] + 34 * mAndN[1]) * multiplier;
                }
                else
                {
                    totalWeight += inputTypes[input.Type] * input.Size;
                }
                if (input.Type.IndexOf("W") >= 0)
                    hasWitness = true;
            }

            foreach (var output in outputs)
            {
                totalWeight += outputTypes[output.Type] * output.Size;
            }
            if (hasWitness)
            {
                totalWeight += 2;
            }
            totalWeight += 10 * 4;
            return (int)Math.Ceiling((decimal)totalWeight / 4);
        }

        
        public decimal CalculateSendCost(int sendOpReturnLength, int inputUtxoSize,
                                 int outputAddressArraySize, string bchChangeAddress, int feeRate = 1, bool forTokens= true)
        {
            var outputs = outputAddressArraySize;
            var nonfeeoutputs = 0;
            if (forTokens)
                nonfeeoutputs = outputAddressArraySize * 546;
            if (!string.IsNullOrEmpty(bchChangeAddress))
                outputs += 1;

            //var fee = this.BITBOX.BitcoinCash.getByteCount({ P2PKH: inputUtxoSize }, { P2PKH: outputs });
            //var fee = GetByteCount(new { P2PKH = inputUtxoSize }, new { P2PKH = outputs });
            var fee = GetByteCount(
                new InOutType[] { new InOutType { Type = "P2PKH", Size = inputUtxoSize } },
                new InOutType[] { new InOutType { Type = "P2PKH", Size = outputs } }
                );
            fee += sendOpReturnLength;
            fee += 10; // added to account for OP_RETURN ammount of 0000000000000000
            fee *= feeRate;
            // console.log("SEND cost before outputs: " + fee.toString());
            fee += nonfeeoutputs;
            //// console.log("SEND cost after outputs are added: " + fee.toString());
            return fee;
        }

        private TransactionDetails ParseSlpOutputScript(byte[] outputScript)
        {
            var slpMsg = new TransactionDetails();

            var chunks = new List<byte[]>();
            try
            {
                chunks = ParseOpReturnToChunks(outputScript);
            }
            catch (Exception)
            {
                throw new Exception("Bad OP_RETURN");
            }
            if( !chunks.Any() )
                throw new Exception("Not SLP");
            if (!chunks[0].SequenceEqual(SD.SlpLokadIdHex))
                throw new Exception("Not SLP");
            if (chunks.Count == 1)
                throw new Exception("Missing token versionType");
            if (chunks[1] == null)
                throw new Exception("Bad versionType buffer");
            //slpMsg.VersionType = chunks[1];
            var version =ParseChunkToInt(chunks[1], 1, 2, true);
            if (!Enum.IsDefined(typeof(SlpVersionType), version))
                throw new Exception("Unsupported token type: " + version);
            slpMsg.VersionType = (SlpVersionType)version;

            if (chunks.Count == 2)
                throw new Exception("Missing SLP transaction type!");
            var transactionType = chunks[2];
            if (transactionType == null)
                throw new Exception("Bad transaction type");
            if (transactionType.SequenceEqual(SlpTransactionType.GENESIS.ToAsciiByteArray()))
                slpMsg.TransactionType = SlpTransactionType.GENESIS;
            else if (transactionType.SequenceEqual(SlpTransactionType.MINT.ToAsciiByteArray()))
                slpMsg.TransactionType = SlpTransactionType.MINT;
            else if (transactionType.SequenceEqual(SlpTransactionType.SEND.ToAsciiByteArray()))
                slpMsg.TransactionType = SlpTransactionType.SEND;
            else
                throw new Exception("Bad transaction type");

            switch (slpMsg.TransactionType)
            {
                case SlpTransactionType.GENESIS:
                    {
                        if (chunks.Count != 10)
                            throw new Exception("GENESIS with incorrect number of parameters!");
                        slpMsg.Symbol = chunks[3]?.ToUtf8() ?? string.Empty;
                        slpMsg.Name = chunks[4]?.ToUtf8() ?? string.Empty;
                        slpMsg.DocumentUri = chunks[5]?.ToUtf8() ?? string.Empty;
                        slpMsg.DocumentSha256 = chunks[6];
                        if (slpMsg.DocumentSha256 != null)
                            if (slpMsg.DocumentSha256.Length != 0 && slpMsg.DocumentSha256.Length != 32)
                                throw new Exception("Token document hash is incorrect length");
                        if (chunks[7] == null)
                        {
                            throw new Exception("Bad decimals buffer");
                        }
                        //slpMsg.Decimals = (Slp.parseChunkToInt(chunks[7]!, 1, 1, true) as number);
                        slpMsg.Decimals = ParseChunkToInt(chunks[7], 1, 1, true).Value;
                        if (slpMsg.VersionType == SlpVersionType.TokenVersionType1_NFT_Child && 
                            slpMsg.Decimals != 0)
                        {
                            throw new Exception("NFT1 child token must have divisibility set to 0 decimal places.");
                        }
                        if (slpMsg.Decimals > 9)
                        {
                            throw new Exception("Too many decimals");
                        }
                        slpMsg.BatonVOut = (byte?)ParseChunkToInt(chunks[8],1,1);
                        if (slpMsg.BatonVOut != null)
                        {
                            if (slpMsg.BatonVOut < 2)
                            {
                                throw new Exception("Mint baton cannot be on vout=0 or 1");
                            }
                            slpMsg.ContainsBaton = true;
                        }
                        if (slpMsg.VersionType == SlpVersionType.TokenVersionType1_NFT_Child && slpMsg.BatonVOut != null)
                            throw new Exception("NFT1 child token must not have a minting baton!");
                        if (chunks[9]==null)
                            throw new Exception("Bad Genesis quantity buffer");
                        if (chunks[9].Length != 8)
                            throw new Exception("Genesis quantity must be provided as an 8-byte buffer");
                        slpMsg.GenesisOrMintQuantity = chunks[9].ToBigNumber();
                        if (slpMsg.VersionType == SlpVersionType.TokenVersionType1_NFT_Child && 
                            slpMsg.GenesisOrMintQuantity != 1)
                        {
                            throw new Exception("NFT1 child token must have GENESIS quantity of 1.");
                        }
                        break;
                    }
                case SlpTransactionType.SEND:
                    {
                        if (chunks.Count < 4)
                            throw new Exception("SEND with too few parameters");
                        if (chunks[3]==null)
                            throw new Exception("Bad tokenId buffer");
                        if (chunks[3].Length != 32)
                            throw new Exception("token_id is wrong length");
                        slpMsg.TokenIdHex = chunks[3].ToHex();
                        // # Note that we put an explicit 0 for  ['token_output'][0] since it
                        // # corresponds to vout=0, which is the OP_RETURN tx output.
                        // # ['token_output'][1] is the first token output given by the SLP
                        // # message, i.e., the number listed as `token_output_quantity1` in the
                        // # spec, which goes to tx output vout=1.
                        //slpMsg.SendOutputs = [];
                        slpMsg.SendOutputs.Add(0);
                        //chunks.slice(4).forEach((chunk) => {
                        chunks.Skip(4).ToList().ForEach((chunk) => {
                            if (chunk==null)
                                throw new Exception("Bad send quantity buffer.");
                            if (chunk.Length != 8)
                                throw new Exception("SEND quantities must be 8-bytes each.");
                            slpMsg.SendOutputs.Add(chunk.ToBigNumber());
                        });
                        // # maximum 19 allowed token outputs, plus 1 for the explicit [0] we inserted.
                        if (slpMsg.SendOutputs.Count < 2)
                        {
                            throw new Exception("Missing output amounts");
                        }
                        if (slpMsg.SendOutputs.Count > 20)
                        {
                            throw new Exception("More than 19 output amounts");
                        }
                        break;
                    }
                case SlpTransactionType.MINT:
                    {
                        if (slpMsg.VersionType == SlpVersionType.TokenVersionType1_NFT_Child)
                        {
                            throw new Exception("NFT1 Child cannot have MINT transaction type.");
                        }
                        if (chunks.Count != 6)
                        {
                            throw new Exception("MINT with incorrect number of parameters");
                        }
                        if (chunks[3] == null)
                        {
                            throw new Exception("Bad token_id buffer");
                        }
                        if (chunks[3].Length != 32)
                        {
                            throw new Exception("token_id is wrong length");
                        }
                        slpMsg.TokenIdHex = chunks[3].ToHex();
                        slpMsg.BatonVOut = (byte?)ParseChunkToInt(chunks[4], 1, 1);
                        if (slpMsg.BatonVOut != null)
                        {
                            if (slpMsg.BatonVOut < 2)
                            {
                                throw new Exception("Mint baton cannot be on vout=0 or 1");
                            }
                            slpMsg.ContainsBaton = true;
                        }
                        if (chunks[5]==null)
                        {
                            throw new Exception("Bad Mint quantity buffer");
                        }
                        if (chunks[5].Length != 8)
                        {
                            throw new Exception("Mint quantity must be provided as an 8-byte buffer");
                        }
                        slpMsg.GenesisOrMintQuantity = chunks[5].ToBigNumber();
                        break;
                    }
                default:
                    throw new Exception("Bad transaction type");
            }
            if (slpMsg.GenesisOrMintQuantity==null && (slpMsg.SendOutputs==null || slpMsg.SendOutputs.Count == 0))
            {
                throw new Exception("SLP message must have either Genesis/Mint outputs or Send outputs, both are missing");
            }
            return slpMsg;
        }
        public int? ParseChunkToInt(byte[] intBytes, int minByteLen, int maxByteLen, bool raiseOnNull = false)
        {
            // # Parse data as unsigned-big-endian encoded integer.
            // # For empty data different possibilities may occur:
            // #      minByteLen <= 0 : return 0
            // #      raise_on_Null == False and minByteLen > 0: return None
            // #      raise_on_Null == True and minByteLen > 0:  raise SlpInvalidOutputMessage
            if ((intBytes == null || intBytes.Length == 0) && !raiseOnNull)
            {
                return null;
            }
            if (intBytes.Length >= minByteLen && intBytes.Length<= maxByteLen)
            {
                return (int)intBytes.ReadBigEndianUInt32();
            }           
            throw new Exception("Field has wrong length");
        }
        private List<byte[]> ParseOpReturnToChunks(byte[] script, bool allowOp0=false, bool allowOpNumber=false)
        {
            // """Extract pushed bytes after opreturn. Returns list of bytes() objects,
            // one per push.
            PushDataOperation[] ops = null;
            // Strict refusal of non-push opcodes; bad scripts throw OpreturnError."""
            try
            {
                ops = this.GetScriptOperations(script);
            }
            catch (Exception e)
            {
                // console.log(e);
                throw new Exception("Script error:" + e.Message);
            }

            if (ops[0].OpCode != (byte)SlpScript.OP_RETURN)
            {
                throw new Exception("No OP_RETURN");
            }
            List<byte[]> chunks = new List<byte[]>();
            
            //process all except first
            ops.TakeLast(ops.Length-1).ToList().ForEach((opitem) => {
                if (opitem.OpCode > (byte)SlpScript.OP_16)
                    throw new Exception("Non-push opcode");
                if (opitem.OpCode > (byte)SlpScript.OP_PUSHDATA4)
                {
                    if (opitem.OpCode == 80)
                    {
                        throw new Exception("Non-push opcode");
                    }
                    if (!allowOpNumber)
                    {
                        throw new Exception("OP_1NEGATE to OP_16 not allowed");
                    }
                    if (opitem.OpCode == (byte)SlpScript.OP_1NEGATE)
                    {
                        opitem.Data = new byte[] { 0x81 };
                    } 
                    else { // OP_1 - OP_16
                        opitem.Data = new byte[] { (byte)(opitem.OpCode - 80) };
                    }
                }
                if (opitem.OpCode == (byte)SlpScript.OP_0 && !allowOp0) {
                    throw new Exception("OP_0 not allowed");
                }
                chunks.Add(opitem.Data);
            });
            // console.log(chunks);
            return chunks;
        }
        private PushDataOperation[] GetScriptOperations(byte[] script)
        {
            var ops = new List<PushDataOperation>();
            try
            {
                int n = 0;
                int dlen;
                while (n < script.Length)
                {
                    var op = new PushDataOperation { OpCode = script[n], Data= null };
                    n += 1;
                    if (op.OpCode <= (byte)SlpScript.OP_PUSHDATA4)
                    {
                        if (op.OpCode < (byte)SlpScript.OP_PUSHDATA1)
                        {
                            dlen = op.OpCode;
                        }
                        else if (op.OpCode == (byte)SlpScript.OP_PUSHDATA1)
                        {
                            dlen = script[n];
                            n += 1;
                        }
                        else if (op.OpCode == (byte)SlpScript.OP_PUSHDATA2)
                        {
                            //dlen = script.slice(n, n + 2).readUIntLE(0, 2);
                            dlen = (int)script.Skip(n).Take(2).ReadLittleEndianUInt32();
                            //dlen = BitConverter.ToInt32( script.Skip(n).Take(2).Reverse().ToArray(), 0 ); //read little endian here                            
                            n += 2;
                        }
                        else
                        {
                            dlen = (int)script.Skip(n).Take(4).ReadLittleEndianUInt32();
                            //dlen = script.slice(n, n + 4).readUIntLE(0, 4);
                            n += 4;
                        }
                        if ((n + dlen) > script.Length)
                        {
                            throw new Exception("IndexError");
                        }
                        if (dlen > 0)
                        {
                            //op.Data = script.slice(n, n + dlen);
                            op.Data = script.Skip(n).Take((int)dlen).ToArray();
                        }
                        n += (int)dlen;
                    }
                    ops.Add(op);
                }
            }
            catch (Exception e)
            {
                // console.log(e);
                throw new Exception("truncated script: " + e.Message);
            }
            return ops.ToArray();
        }

        SlpTransaction ParseSlpTransaction(NBitcoin.Transaction tr)
        {
            var outputScriptAt0 = tr.Outputs.First().ScriptPubKey.ToBytes();
            TransactionDetails slpDetails;
            try
            {
                slpDetails = ParseSlpOutputScript(outputScriptAt0);
                if (slpDetails == null) //invalid OP_RETURN structure
                    return null;
            }
            catch (Exception)
            {
                _log.LogDebug("[ERROR]: Invalid slp transaction OP_RETURN data {}", tr.GetHash().ToString());
                return null;
            }
            
            try
            {
                switch (slpDetails.TransactionType)
                {
                    case SlpTransactionType.GENESIS:
                        return ParseGenesis(tr, slpDetails);
                    case SlpTransactionType.MINT:
                        return ParseMint(tr, slpDetails);
                    case SlpTransactionType.SEND:
                        return ParseSend(tr, slpDetails);
                    default:
                        throw new Exception("Invalid transaction type!");
                }
            }
            catch (Exception e)
            {
                _log.LogError("Exception at transaction {0}. {1}", tr.GetHash().ToString(), e.Message);
                throw;
            }
        }

        SlpTransaction ParseSend(Transaction tr, TransactionDetails slpDetails)
        {
            // this cache is not mandatory at parse phase since we also store hash data - id is then reconnected for faster index performanced
            var slpSendTransaction = new SlpTransaction()
            {
                //Id = dbSlpIdManager.GetNextSlpTransactionId(), //todo from id segment manager
                SlpTokenId = slpDetails.TokenIdHex.FromHex(),
                Type = SlpTransactionType.SEND,
                SlpTokenType = slpDetails.VersionType,
                Hash = tr.GetHash().ToBytes(false),
            };
            slpSendTransaction.SlpTransactionOutputs.Clear();

            //output at 0 - OP_RETURN - zero
            slpSendTransaction.SlpTransactionOutputs.Add(OpReturnOutput0(slpSendTransaction));
            for (int i = 1; i < tr.Outputs.Count; i++)
            {
                if (i >= slpDetails.SendOutputs.Count) //bitcoincash
                {
                    //slp op_return specified send outputs does not have this outputs marker as token output so we add it as bchoutput
                    var bchOutput = BchOutput(slpSendTransaction, tr, i, slpDetails);
                    slpSendTransaction.SlpTransactionOutputs.Add(bchOutput);
                    slpSendTransaction.TokenOutputSum += bchOutput.Amount;
                }
                else //simpleledger address
                {
                    var sendOutput = SendOutput(slpSendTransaction, tr, i, slpDetails);
                    slpSendTransaction.TokenOutputSum += sendOutput.Amount;
                    slpSendTransaction.SlpTransactionOutputs.Add(sendOutput);
                }
            }
            //from specs this will go into validate slp in second phase: 
            // if transaction outputs count is lower that token transaction outputs specified in OP_RETURN then we must add the sum from OP_RETURN to total output sum
            for (int i = tr.Outputs.Count; i < slpDetails.SendOutputs.Count; i++) //any additional bch tokens contribute 0 slp token 
                slpSendTransaction.TokenOutputSum += (ulong)slpDetails.SendOutputs.ElementAt(i);
            if (tr.Inputs.Count == 0)
                throw new Exception("SEND transaction must have at least one input.");

            //technically this code could be run at the pass 2 but if output is already present at phase 1 we can validate without issues
            slpSendTransaction.SlpTransactionInputs.Clear();
            for (int i = 0; i < tr.Inputs.Count; i++)
            {
                var input = tr.Inputs.ElementAt(i);
                var sourceTrHash = input.PrevOut.Hash;

                //SlpTransactionOutput validSourceOutput = null;
                //store input in database even if we do not have source transaction yet
                var sendInput = new SlpTransactionInput()
                {
                    // Id = dbSlpIdManager.GetNextSlpTransactionInputId(),
                    // Address = null, validSourceOutput?.Address,
                    // BlockchainSatoshis = validSourceOutput?.BlockchainSatoshis ?? 0,
                    SlpTransactionId = slpSendTransaction.Id,
                    VOut = (int)input.PrevOut.N,
                    SourceTxHash = sourceTrHash.ToBytes(false)
                    // SlpSourceTransactionOutputId = validSourceOutput?.Id
                };
                slpSendTransaction.SlpTransactionInputs.Add(sendInput);
            }
            return slpSendTransaction;
        }

        SlpTransaction ParseMint(Transaction tr, TransactionDetails slpDetails)
        {
            var slpMintTransaction = new SlpTransaction()
            {
                //Id = dbSlpIdManager.GetNextSlpTransactionId(), //todo from id segment manager
                //SlpTokenId = ?
                SlpTokenId = slpDetails.TokenIdHex.ToLower().FromHex(),
                Type = SlpTransactionType.MINT,
                SlpTokenType = slpDetails.VersionType,
                Hash = tr.GetHash().ToBytes(false),
                MintBatonVOut = slpDetails.BatonVOut,
                AdditionalTokenQuantity = slpDetails.GenesisOrMintQuantity.Value
            };

            //OUTPUTS
            //output at 0 - OP_RETURN - zero
            slpMintTransaction.SlpTransactionOutputs.Add(OpReturnOutput0(slpMintTransaction));
            slpMintTransaction.SlpTransactionOutputs.Add(MintReceiverOutput1(slpMintTransaction, tr, slpDetails));
            //output at index MintBatonVOut - mint baton receiver
            for (int i = 2; i < tr.Outputs.Count; i++)
            {
                if (slpMintTransaction.MintBatonVOut.HasValue && slpMintTransaction.MintBatonVOut == i)
                    slpMintTransaction.SlpTransactionOutputs.Add(MintBatonOutput(slpMintTransaction, tr, i, slpDetails));
                else
                    slpMintTransaction.SlpTransactionOutputs.Add(BchOutput(slpMintTransaction, tr, i, slpDetails));
            }
            //find baton input - can be previous mint or genesis transation that contains valid baton
            for (int i = 0; i < tr.Inputs.Count; i++)
            {
                var input = tr.Inputs.ElementAt(i);
                var sourceTrHash = input.PrevOut.Hash;
                // SlpTransactionOutput validSourceOutput = null; //TODO: connect with cache outputs 
                var sendInput = new SlpTransactionInput()
                {
                    //Id = dbSlpIdManager.GetNextSlpTransactionInputId(),
                    // Address = validSourceOutput?.Address,
                    // BlockchainSatoshis = validSourceOutput?.BlockchainSatoshis ?? 0,
                    // SlpAmount = validSourceOutput?.SlpAmmount ?? 0,
                    SlpTransactionId = slpMintTransaction.Id,
                    VOut = (int)input.PrevOut.N,
                    SourceTxHash = sourceTrHash.ToBytes(false)
                    //SlpSourceTransactionOutputId = validSourceOutput?.Id
                };
                slpMintTransaction.SlpTransactionInputs.Add(sendInput);
            }
            return slpMintTransaction;
        }

        SlpTransaction ParseGenesis(NBitcoin.Transaction tr, TransactionDetails slpDetails)
        {
            var trHashHex = tr.GetHash().ToString();
            var slpToken = new SlpToken()
            {
                Decimals = (byte)slpDetails.Decimals,
                DocumentUri = slpDetails.DocumentUri,
                Name = slpDetails.Name,
                Symbol = slpDetails.Symbol,
                Hash = trHashHex.FromHex(),
                VersionType = slpDetails.VersionType,
                DocumentSha256Hex = slpDetails.DocumentSha256?.ToHex(),
            };

            var slpTransaction = new SlpTransaction()
            {
                // not set yet Id = ...
                SlpToken = slpToken,
                SlpTokenId = trHashHex.FromHex(),
                Type = SlpTransactionType.GENESIS,
                SlpTokenType = slpDetails.VersionType,
                AdditionalTokenQuantity = slpDetails.GenesisOrMintQuantity.Value,
                MintBatonVOut = slpDetails.BatonVOut,
                // TransactionId = tr.Id,
                Hash = trHashHex.FromHex(),
                State = SlpTransaction.TransactionState.SLP_VALID, //self evidently valid transactions
            };

            for (int i = 0; i< tr.Inputs.Count; i++)
            {
                var input = tr.Inputs[i];
                var slpInput = new SlpTransactionInput
                {
                    SourceTxHash = input.PrevOut.Hash.ToBytes(false),
                    VOut = (int)input.PrevOut.N,
                    Address = null,
                    BlockchainSatoshis = 0,
                    SlpAmount = 0
                };
                slpTransaction.SlpTransactionInputs.Add(slpInput);
            }

            //output at 0 - OP_RETURN - zero
            slpTransaction.SlpTransactionOutputs.Add(OpReturnOutput0(slpTransaction));
            //output at 1 - initial mint receiver
            slpTransaction.SlpTransactionOutputs.Add(MintReceiverOutput1(slpTransaction, tr, slpDetails));
            for (int i = 2; i < tr.Outputs.Count; i++)
            {
                //output at index MintBatonVOut - minto baton receiver
                if (slpTransaction.MintBatonVOut.HasValue && slpTransaction.MintBatonVOut.Value == i &&
                    slpTransaction.MintBatonVOut.Value < tr.Outputs.Count)
                    slpTransaction.SlpTransactionOutputs.Add(MintBatonOutput(slpTransaction, tr, i, slpDetails));
                else
                    slpTransaction.SlpTransactionOutputs.Add(BchOutput(slpTransaction, tr, i, slpDetails));
            }
            //if (slpTransaction.MintBatonVOut.HasValue && slpTransaction.MintBatonVOut.Value >= tr.Outputs.Count)
            //    throw new Exception($"Transaction {tr.GetHash()} does not have output present at baton position {slpTransaction.MintBatonVOut.Value}");
            return slpTransaction;
        }

        private SlpTransactionOutput SendOutput(SlpTransaction slpTr, Transaction tr, int outputIndex, TransactionDetails slpDetails)
        {
            var sendReceiverOutput = tr.Outputs.ElementAt(outputIndex);
            var slpPrefix = _rpcClient.Network.ChainName.GetSlpPrefix();
            var sendAddress = CashAddressExtensions.EncodeBCashAddressFromScriptPubKey(slpPrefix, sendReceiverOutput.ScriptPubKey);
            var sendOutput = new SlpTransactionOutput()
            {
                //Id = dbSlpIdManager.GetNextSlpTransactionOutputId(),
                BlockchainSatoshis = sendReceiverOutput.Value.ToDecimal(MoneyUnit.Satoshi),
                //Address = sendAddress,
                Address = new SlpAddress { Address = sendAddress, BlockHeight = slpTr.BlockHeight },
                VOut = outputIndex,
                Amount = (ulong)slpDetails.SendOutputs[outputIndex],
                SlpTransaction = slpTr,
                //SlpTransactionHex = tr.GetHash().ToString()
            };
            return sendOutput;
        }

        private SlpTransactionOutput OpReturnOutput0(SlpTransaction slpTx)
        {
            var opReturnOutput = new SlpTransactionOutput()
            {
                // Id = dbSlpIdManager.GetNextSlpTransactionOutputId(),
                BlockchainSatoshis = 0,
                VOut = 0,
                //Address = SD.UnparsedAddress,
                Address = new SlpAddress { Address = SD.UnparsedAddress, BlockHeight = slpTx.BlockHeight },
                Amount = 0,
                SlpTransaction = slpTx
                //SlpTransactionHex = slpTransaction.Hex
            };
            return opReturnOutput;
        }

        private SlpTransactionOutput MintReceiverOutput1(SlpTransaction slpTr, NBitcoin.Transaction tr, TransactionDetails slpDetails)
        {
            const int outputIndex1 = 1;
            if (tr.Outputs.Count <= outputIndex1)
                throw new IndexOutOfRangeException("Slp transaction output out of range!");
            var initialMintReciverOutput = tr.Outputs.ElementAt(outputIndex1);
            var outputAddress = CashAddressExtensions.EncodeBCashAddressFromScriptPubKey(_rpcClient.Network.ChainName.GetSlpPrefix(), initialMintReciverOutput.ScriptPubKey);
            var slpMintReceiverOutput = new SlpTransactionOutput()
            {
                // Id = dbSlpIdManager.GetNextSlpTransactionOutputId(),
                BlockchainSatoshis = initialMintReciverOutput.Value.ToDecimal(NBitcoin.MoneyUnit.Satoshi),
                //Address = new Address{outputAddress,
                Address = new SlpAddress { Address=outputAddress, BlockHeight = slpTr.BlockHeight },
                VOut = outputIndex1,
                Amount = slpDetails.GenesisOrMintQuantity.Value,
                SlpTransaction = slpTr,
                //SlpTransactionHex = tr.GetHash().ToString()
            };
            return slpMintReceiverOutput;
        }

        private SlpTransactionOutput MintBatonOutput(SlpTransaction slpTr, NBitcoin.Transaction tr, int outputIndex, TransactionDetails slpDetails)
        {
            var mintOutput = tr.Outputs.ElementAt(outputIndex);
            var mintBatonaddress = CashAddressExtensions.EncodeBCashAddressFromScriptPubKey(
                _rpcClient.Network.ChainName.GetSlpPrefix(),
                mintOutput.ScriptPubKey);

            //var amount = 0m;
            //if (outputIndex < slpDetails.SendOutputs.Count)
            //    amount = slpDetails.SendOutputs[outputIndex];

            var mintBatonOutput = new SlpTransactionOutput()
            {
                // Id = dbSlpIdManager.GetNextSlpTransactionOutputId(),
                BlockchainSatoshis = mintOutput.Value.ToDecimal(NBitcoin.MoneyUnit.Satoshi),
                //Address = mintBatonaddress,,
                Address = new SlpAddress { Address=mintBatonaddress, BlockHeight = slpTr.BlockHeight },
                VOut = outputIndex,
                Amount = 0, //baton receiver recieves 0 tokens and can produce another token in the subsequent tx
                SlpTransaction = slpTr,
                //SlpTransactionHex = tr.GetHash().ToString()
            };
            return mintBatonOutput;
        }

        private SlpTransactionOutput BchOutput(SlpTransaction slpTr, NBitcoin.Transaction tr, int outputIndex, TransactionDetails slpDetails)
        {
            var output = tr.Outputs.ElementAtOrDefault(outputIndex);
            var bchAddr = output.ScriptPubKey.GetDestinationAddress(_rpcClient.Network);
            if (bchAddr == null) //another type of spubkey not handled with strange data 1 342354535
            {
                var multiSigTemplateParameters = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(output.ScriptPubKey);
                if (multiSigTemplateParameters == null)
                    throw new Exception($"Failed to extract address of tx {slpTr.Hash.ToHex()} at output {outputIndex}");
                //now what? use first pub key to generate address and verify with bch team
                var pubKey = multiSigTemplateParameters.PubKeys.First();
                bchAddr = pubKey.GetAddress(ScriptPubKeyType.Legacy, _rpcClient.Network);
                if (bchAddr == null)
                    throw new Exception($"Failed to extract address from multisig first public key at tx {slpTr.Hash.ToHex()} at output {outputIndex}");
            }           
            var emptyNonSlpOutput = new SlpTransactionOutput()
            {
                BlockchainSatoshis = tr.Outputs.ElementAt(outputIndex).Value.ToDecimal(NBitcoin.MoneyUnit.Satoshi),
                //Address = bchAddr.ToString(),
                Address = new SlpAddress { Address = bchAddr.ToString(), BlockHeight = slpTr.BlockHeight },
                VOut = outputIndex,
                Amount = 0,
                SlpTransaction = slpTr,
                //SlpTransactionHex = tr.GetHash().ToString()
            };
            return emptyNonSlpOutput;
        }

        #endregion

        #region STATIC
        public static bool PreSendSlpJudgementCheck(AddressUtxoResult txo, string tokenId)
        {
            if (txo.SlpUtxoJudgement == SlpUtxoJudgement.UNKNOWN)
                throw new Exception("There at least one input UTXO that does not have a proper SLP judgement");
            if (txo.SlpUtxoJudgement == SlpUtxoJudgement.UNSUPPORTED_TYPE)
                throw new Exception("There is at least one input UTXO that is an Unsupported SLP type.");
            if (txo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_BATON)
                throw new Exception("There is at least one input UTXO that is a baton. You can only spend batons in a MINT transaction.");
            if (txo.TransactionDetails != null)
            {
                if (txo.SlpUtxoJudgement == SlpUtxoJudgement.SLP_TOKEN)
                {
                    if (!txo.SlpUtxoJudgementAmount.HasValue)
                        throw new Exception("There is at least one input token that does not have the 'slpUtxoJudgementAmount' property set.");
                    if (txo.TransactionDetails.TokenIdHex != tokenId)
                        throw new Exception("There is at least one input UTXO that is a different SLP token than the one specified.");
                    return txo.TransactionDetails.TokenIdHex == tokenId;
                }
            }
            return false;
        }


        #endregion
    }
}

