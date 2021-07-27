using Slp.Common.Extensions;
using Slp.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using Slp.Common.Interfaces;
using Slp.Common.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Slp.Common.DataAccess;
using Slp.Common.Models.DbModels;
using System.Globalization;

namespace Slp.Common.Services
{
    public class SlpDataService : ISlpDataService
    {
        private readonly ILogger<SlpDataService> _log;
        private readonly RPCClient _rpcClient;
        private readonly SlpDbContext _slpDb;
        public SlpDataService(
            ILogger<SlpDataService> log, 
            RPCClient rpcClient, 
            SlpDbContext slpDb
            )
        {
            _rpcClient = rpcClient;
            _log = log;
            _slpDb = slpDb;
        }

        #region API
        public async Task<string> GetTokenActiveMintTxAsync(string tokenId)
        {
            var lastMint = await _slpDb.SlpTransaction
                        .Where(t => t.SlpTokenId == tokenId.FromHex() && t.Type == Slp.Common.Models.Enums.SlpTransactionType.MINT)
                        .OrderByDescending(t => t.Id)
                        .Select(t => t.Hash)
                        .FirstOrDefaultAsync();
            return lastMint.ToHex();
        }

        public async Task<int> GetTokenTransactionCount(string tokenId)
        {
            var tokenTxCount = await _slpDb.SlpTransaction
                        .Where(t => t.SlpTokenId == tokenId.FromHex() && t.Type == Slp.Common.Models.Enums.SlpTransactionType.SEND)
                        .CountAsync();
            return tokenTxCount + 1;
        }
        public async Task<int> GetValidTokenAddressesCount(string tokenId)
        {
            var prefix = _rpcClient.GetSlpPrefix().ToString();
            var tokenTxCount = await _slpDb.SlpTransactionOutput
                               .Include(o => o.SlpTransaction)
                               .Include(o => o.Address)
                               .Where(t => t.SlpTransaction.SlpTokenId == tokenId.FromHex() &&
                                    !t.NextInputId.HasValue &&
                                    t.Address.Address.StartsWith(prefix) &&
                                    t.SlpTransaction.Type == Slp.Common.Models.Enums.SlpTransactionType.SEND &&
                                    t.SlpTransaction.State == SlpTransaction.TransactionState.SLP_VALID)
                               .Select(o => o.Address.Address)
                                .Distinct()
                                .CountAsync();
            return tokenTxCount;
        }

        public async Task<decimal> GetTokenTotalMintedCount(string tokenId)
        {
            var tokenDecimals = await _slpDb.SlpToken.Where(t => t.Hash == tokenId.FromHex()).Select(t => t.Decimals).FirstAsync();
            var tokenTotalMinted = await _slpDb.SlpTransactionOutput
                               .Include(o => o.SlpTransaction)
                               .Where(o => o.SlpTransaction.SlpTokenId == tokenId.FromHex() && 
                                        o.SlpTransaction.State == SlpTransaction.TransactionState.SLP_VALID &&
                                        o.SlpTransaction.Type == Slp.Common.Models.Enums.SlpTransactionType.MINT
                               )
                               .Select(o => (o.SlpTransaction.AdditionalTokenQuantity ?? 0))
                               .SumAsync();
            return tokenTotalMinted.ToTokenValue(tokenDecimals);
        }
        /// <summary>
        /// Token can be burned in two ways. Not all slp inputs are spent in outputs meaning remaining tokens are burned. Or we can burn token in an invalid SLP transaction or 
        /// spending utxo in bch transaction.
        /// </summary>
        public async Task<decimal> GetTokenTotalBurned(string tokenId)
        {
            var tokenDecimals = await _slpDb.SlpToken.Where(t => t.Hash == tokenId.FromHex()).Select(t => t.Decimals).FirstAsync();
            var burnedWithTransactions = await _slpDb.SlpTransactionOutput
                               .Include(o => o.SlpTransaction)
                               .Include(o => o.NextInput)
                                    .ThenInclude(o => o.SlpTransaction)
                               .Where(o => o.SlpTransaction.SlpTokenId == tokenId.FromHex() && o.NextInput != null && o.NextInput.SlpTransaction.Type == Slp.Common.Models.Enums.SlpTransactionType.BURN)
                               .Select(o => o.Amount)
                               .SumAsync();
            // burned in transactions input > total token outputs
            //here we must fix indexser so total inputs and outputs for transaction are calculated so we can query easily
            return burnedWithTransactions.ToTokenValue(tokenDecimals);
        }

        public async Task<SlpTransaction> GetTokenLastActiveSend(string tokenId)
        {
            var burnedWithTransactions = await _slpDb.SlpTransaction
                               .Include(o => o.SlpToken)
                               .Where(o => o.SlpTokenId == tokenId.FromHex() && o.Type == Slp.Common.Models.Enums.SlpTransactionType.SEND && o.State == SlpTransaction.TransactionState.SLP_VALID)
                               .OrderByDescending(o => o.Id)
                               .FirstAsync();
            return burnedWithTransactions;
        }
        public async Task<int> GetValidTokenUtxoCount(string tokenId)
        {
            var prefix = _rpcClient.GetSlpPrefix().ToString();
            var tokenTxCount = await _slpDb.SlpTransactionOutput
                               .Include(o => o.SlpTransaction)
                               .Include(o => o.Address)
                               .Where(t => t.SlpTransaction.SlpTokenId == tokenId.FromHex() &&
                                    !t.NextInputId.HasValue &&
                                    t.Address.Address.StartsWith(prefix) &&
                                    t.SlpTransaction.Type == Slp.Common.Models.Enums.SlpTransactionType.SEND &&
                                    t.SlpTransaction.State == SlpTransaction.TransactionState.SLP_VALID)
                                .CountAsync();
            return tokenTxCount;
        }

        public async Task<decimal> GetCirculatingSupply(string tokenId)
        {
            var tokenDecimals = await _slpDb.SlpToken.Where(t => t.Hash == tokenId.FromHex()).Select(t => t.Decimals).FirstAsync();
            var circulatingSupply = await _slpDb.SlpTransactionOutput
                               .Include(o => o.SlpTransaction)
                               .Where(o => o.SlpTransaction.SlpTokenId == tokenId.FromHex() &&
                                        o.SlpTransaction.Type == Slp.Common.Models.Enums.SlpTransactionType.SEND &&
                                        o.SlpTransaction.State == SlpTransaction.TransactionState.SLP_VALID &&
                                        !o.NextInputId.HasValue //utxo
                                        )
                               .Select(s=>s.Amount)
                               .SumAsync();
            return circulatingSupply.ToTokenValue(tokenDecimals);
        }

        public async Task<int> GetValidAddressesCount(string tokenId)
        {
            var prefix = _rpcClient.GetSlpPrefix().ToString();
            var validAddressesCount = await _slpDb.SlpTransactionOutput
                               .Include(o => o.SlpTransaction)
                               .Include(o => o.Address)
                               .Where(o => o.SlpTransaction.SlpTokenId == tokenId.FromHex() &&
                                        o.SlpTransaction.Type == Slp.Common.Models.Enums.SlpTransactionType.SEND &&
                                        o.SlpTransaction.State == SlpTransaction.TransactionState.SLP_VALID &&
                                        !o.NextInputId.HasValue && //utxo 
                                        o.Address.Address.StartsWith(prefix)
                                        )
                               .Select(a=>a.Address.Address)
                               .Distinct()
                               .CountAsync();
            return validAddressesCount;
        }

        public async Task<TokenViewModel[]> GetTokenInformationAsync(string[] tokenIds)
        {
            //token aggregates 
            var tokens = tokenIds.Select(t => t.FromHex());
            var tokenDataArray = await _slpDb.SlpTransaction
                .Include(t => t.SlpToken)
                .Include(t => t.Block)
                .Where(t => tokens.Contains(t.Hash)) //sql contains will work on byte level
                .Select(
                    s => new TokenViewModel()
                    {
                        Decimals = s.SlpToken.Decimals,
                        DocumentUri = s.SlpToken.DocumentUri,
                        DocumentHash = s.SlpToken.DocumentSha256Hex,
                        Symbol = s.SlpToken.Symbol,
                        Name = s.SlpToken.Name,
                        ContainsBaton = s.MintBatonVOut.HasValue,
                        Id = s.Hash.ToHex(),
                        InitialTokenQtyInSatoshis = s.AdditionalTokenQuantity ?? 0m,
                        MintingBatonStatus = s.MintBatonVOut.HasValue ? "MINTED" : "NEVER_CREATED",
                        BlockCreated = s.BlockHeight ?? -1,
                        Timestamp = s.Block.BlockTime.ToString(),
                        TimestampUnix = s.Block.BlockTime.ToUnixTimestamp(),
                        //all this stats can be retrieved by others api-a. There is no reason to fetch all this in single call
                        ActiveMint = null,
                        BlockLastActiveMint = null,
                        BlockLastActiveSend = null,
                        CirculatingSupply = -1, //genesis + mints - burns
                        LastActiveSend = -1,
                        VersionType = (int)s.SlpTokenType,
                        //Quantity = Initial token quantity,
                        SatoshisLockedUp = 0m,
                        TotalBurned = 0,
                        TotalMinted = 0,
                        ValidTokenUtxos = 0,
                        TxnsSinceGenesis = 0,
                        ValidAddresses = 0
                    }
                )
                .ToArrayAsync();

            foreach (var token in tokenDataArray)
            {
                
                var lastTx= await GetTokenLastActiveSend(token.Id);
                token.BlockLastActiveSend = lastTx.BlockHeight ?? -1;
                token.LastActiveSend = lastTx.BlockHeight ?? -1;
                
                token.TotalBurned = await GetTokenTotalBurned(token.Id);
                token.TotalMinted = await GetTokenTotalMintedCount(token.Id);
                
                token.ValidTokenUtxos = await GetValidTokenUtxoCount(token.Id);
                token.CirculatingSupply = await GetCirculatingSupply(token.Id);
                token.ValidAddresses = await GetValidAddressesCount(token.Id);
                token.TxnsSinceGenesis = await GetTokenTransactionCount(token.Id);
            }
            
            return tokenDataArray;
        }



        public async Task<AddressBalance> GetTokenBalancesAsync(string slpAddress, string tokenId)
        {
            if (!slpAddress.IsSlpAddress())
                throw new Exception("Only slp address is needed!");
            var token = _slpDb.SlpToken.First(t => t.Hash == tokenId.FromHex());
            var utxoAddressBalance = await _slpDb.SlpTransactionOutput
                                    .Include(o => o.SlpTransaction)
                                        .ThenInclude(o => o.SlpToken)
                                    .Include(o => o.Address)
                                    .Where(o => o.Address.Address == slpAddress && o.SlpTransaction.SlpTokenId == tokenId.FromHex() && !o.NextInputId.HasValue)
                                    .SumAsync(o => o.Amount);
            return new AddressBalance
            {
                Balance = utxoAddressBalance.ToTokenValue(token.Decimals),
                BalanceString = utxoAddressBalance.ToTokenValue(token.Decimals).ToString(),
                SlpAddress = slpAddress,
                BchAddress = slpAddress.ToPrefixedBchAddress(),
                LegacyAddress = slpAddress.ToLegacyAddress(),
                DecimalCount = token.Decimals,
                TokenId = token.Hash.ToHex()
            };
        }
        public async Task<AddressBalance[]> GetAddressBalancesAsync(string address)
        {
            var slpAddress = address.ToPrefixedSlpAddress();
            var utxosForAddress = await _slpDb.SlpTransactionOutput
                                    .Include(o => o.Address)
                                    .Include(o => o.SlpTransaction)
                                        .ThenInclude(t => t.SlpToken)
                                    .Where(o => o.Address.Address == slpAddress && !o.NextInputId.HasValue)
                                    .ToListAsync();
            var res = new List<AddressBalance>();
            foreach (var utxo in utxosForAddress)
            {
                var a = res.Find(a => a.SlpAddress == utxo.Address.Address);
                if (a == null)
                {
                    a = new AddressBalance()
                    {
                        SlpAddress = utxo.Address.Address,
                        BchAddress = utxo.Address.Address.ToPrefixedBchAddress(),
                        LegacyAddress = utxo.Address.Address.ToLegacyAddress(),
                        Balance = utxo.Amount.ToTokenValue(utxo.SlpTransaction.SlpToken.Decimals),
                        DecimalCount = utxo.SlpTransaction.SlpToken.Decimals,
                        TokenId = utxo.SlpTransaction.SlpTokenId.ToHex(),
                        BalanceString = utxo.Amount.ToTokenValue(utxo.SlpTransaction.SlpToken.Decimals).ToString()
                    };
                    res.Add(a);
                }
                else
                {
                    a.Balance += utxo.Amount;
                    a.BalanceString = a.Balance.ToString();
                }
            }
            return res.ToArray();
        }


        public async Task<TokenBalanceData[]> GetTokenBalancesAsync(string tokenId)
        {
            var slpPrefix = _rpcClient.GetSlpPrefix().ToString();
            var utxoForToken = await _slpDb.SlpTransactionOutput
                                    .Include(o => o.SlpTransaction)
                                    .Include(o => o.Address)
                                    .Where(o => o.SlpTransaction.SlpTokenId == tokenId.FromHex() && !o.NextInputId.HasValue && o.Address.Address.StartsWith(slpPrefix))
                                    .OrderBy(o=>o.Address.Address)
                                    .Select(o => new TokenBalanceData
                                    {
                                        TokenBalance = o.Amount,
                                        TokenBalanceString = o.Amount.ToString(),
                                        SlpAddress = o.Address.Address,
                                        TokenId = o.SlpTransaction.SlpTokenId.ToHex()
                                    })                                    
                                    .ToArrayAsync();
            var decimals = await _slpDb.SlpToken.Where(t => t.Hash == tokenId.FromHex()).Select(t=>t.Decimals).FirstAsync();
            var result = utxoForToken.GroupBy(u => u.SlpAddress).Select(g => new TokenBalanceData
            {
                SlpAddress = g.Key,
                TokenBalance = g.Sum(t => t.TokenBalance).ToTokenValue(decimals),
                TokenBalanceString = g.Sum(t => t.TokenBalance).ToTokenValue(decimals).ToString(),
                TokenId = g.First().TokenId
            }).ToArray();
            return result;
        }

        static ValidationResult GetValidationResult(SlpTransaction slpTx)
        {
            if (slpTx == null)
                return new ValidationResult { TxId = null, Valid = false, Reason = "Not a SLP transaction." };
            if (slpTx.State == Slp.Common.Models.DbModels.SlpTransaction.TransactionState.SLP_VALID)
                return new ValidationResult { TxId = slpTx.Hash.ToHex(), Valid = true, Reason = "Valid SLP transaction." };
            if (slpTx.State == Slp.Common.Models.DbModels.SlpTransaction.TransactionState.SLP_INVALID)
                return new ValidationResult { TxId = slpTx.Hash.ToHex(), Valid = false, Reason = slpTx.InvalidReason };
            if (slpTx.State == Slp.Common.Models.DbModels.SlpTransaction.TransactionState.SLP_UNKNOWN)
                return new ValidationResult { TxId = slpTx.Hash.ToHex(), Valid = false, Reason = "Not yet determined. Validation not yet finished." };
            return new ValidationResult { TxId = slpTx.Hash.ToHex(), Valid = false, Reason = "Unknown reason." };
        }

        public async Task<ValidationResult> ValidateTransactionAsync(string txId)
        {
            var slpTx = await _slpDb.SlpTransaction.FirstOrDefaultAsync(t => t.Hash == txId.FromHex());
            return GetValidationResult(slpTx);
        }

        public async Task<ValidationResult[]> ValidateTransactionsAsync(TxIds txIds)
        {
            var byteArr = txIds.txids.ToBytes();
            var slpTxs = await _slpDb.SlpTransaction.Where(t => byteArr.Contains(t.Hash)).ToListAsync();
            return slpTxs.Select(s => GetValidationResult(s)).ToArray();
        }

        public async Task<TxDetails> GetTransactionDetails(string txId)
        {
            var nodeTxTask = _rpcClient.GetRawTransactionAsync(new uint256(txId));
            var countTask = _rpcClient.GetBlockCountAsync();
            var slpTxTask = _slpDb.SlpTransaction
                             .Include(t => t.SlpTransactionInputs)
                             .Include(t => t.SlpTransactionOutputs)
                                .ThenInclude(t => t.NextInput)
                                    .ThenInclude(t => t.SlpTransaction)
                              .Include(t => t.SlpTransactionOutputs)
                                .ThenInclude(t => t.Address)
                             .Include(t => t.SlpToken)
                             .Where(t => t.Hash == txId.FromHex())
                             .OrderBy(s => s.Id)
                             .Select(s => new { s.Hash, s.SlpTokenId, s.SlpToken.Decimals, s.BlockHeight, s.SlpTransactionInputs, s.SlpTransactionOutputs, s.State, s.Type })
                             .FirstAsync();
            await Task.WhenAll(nodeTxTask, slpTxTask, countTask);

            var slpTx = slpTxTask.Result;
            var bchTx = nodeTxTask.Result;
            var blockCount = countTask.Result;

            SlpBlock block = null;
            int confirmations = 0;
            if (slpTx.BlockHeight.HasValue)
            {
                block = await _slpDb.SlpBlock.FirstAsync(b => b.Height == slpTx.BlockHeight);
                confirmations = blockCount - block.Height;
            }

            var res = new TxnDetailsDeep
            {
                TxId = txId,
                BlockHash = block?.Hash.ToHex() ?? null,
                BlockHeight = block?.Height ?? -1,
                BlockTime = block.BlockTime.ToUnixTimestamp(),
                Time = block.BlockTime.ToUnixTimestamp(),
                Confirmation = confirmations,
                IsCoinbase = bchTx.IsCoinBase,
                LockTime = bchTx.LockTime.Value,
                Version = bchTx.Version,
                Size = bchTx.GetSerializedSize(),
                ValueOut = Money.Satoshis(slpTx.SlpTransactionOutputs.Sum(o => o.BlockchainSatoshis)).ToDecimal(MoneyUnit.BTC),
                ValueIn = Money.Satoshis(slpTx.SlpTransactionInputs.Sum(i => i.BlockchainSatoshis)).ToDecimal(MoneyUnit.BTC),
                VOut = new VOut[slpTx.SlpTransactionOutputs.Count],
                VIn = new VIn[slpTx.SlpTransactionInputs.Count],
                //TODO: Fees = nodeTxTask.Result.GetFee(nodeTxTask.Result.Outputs);
            };
            var tokenInfo = new TokenInfo()
            {
                TokenIdHex = slpTx.SlpTokenId.ToHex(),
                TokenIsValid = slpTx.State == SlpTransaction.TransactionState.SLP_VALID,
                VersionType = 1, //TODO: read from db or tx
                TransactionType = slpTx.Type.ToString()
            };
            for (var i = 0; i < slpTx.SlpTransactionOutputs.Count; i++)
            {
                var spent = slpTx.SlpTransactionOutputs[i].NextInput;
                var spentIx = spent?.SlpTransaction?.SlpTransactionInputs.OrderBy(t=>t.Id).ToList().IndexOf(spent);
                var addr = slpTx.SlpTransactionOutputs[i].Address?.Address;
                string[] addrArr = null, cashAddrArr = null;
                if (addr != null && addr!= SD.UnparsedAddress)
                {
                    addrArr = new string[] { addr.ToLegacyAddress() };
                    cashAddrArr = new string[] { addr.ToPrefixedBchAddress() };
                }
                res.VOut[i] = new VOut
                {
                    Value = Money.Satoshis(slpTx.SlpTransactionOutputs[i].BlockchainSatoshis).ToDecimal(MoneyUnit.BTC).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    N = i,
                    ScriptPubKey = new ScriptPubKey
                    {
                        Hex = bchTx.Outputs[i].ScriptPubKey.ToBytes().ToHex(),
                        Asm = bchTx.Outputs[i].ScriptPubKey.ToString(),
                        Addresses = addrArr,
                        Type = "TODO: determine strings",
                        CashAddrs = cashAddrArr
                    },
                    SpentHeight = spent?.SlpTransaction.BlockHeight,
                    SpentIndex = spentIx,
                    SpentTxId = spent?.SlpTransaction.Hash.ToHex(),
                };
                //if (slpTx.SlpTransactionOutputs[i].Address?.IsSlpAddress() ?? false)
                if (slpTx.SlpTransactionOutputs[i].Address.Address.IsSlpAddress() )
                {
                    var amount = slpTx.SlpTransactionOutputs[i].Amount.ToTokenValue(slpTx.Decimals);
                    tokenInfo.SendOutputs.Add(amount.ToString(CultureInfo.InvariantCulture));
                }
            }
            for (var i = 0; i < slpTx.SlpTransactionInputs.Count; i++)
            {
                var input = slpTx.SlpTransactionInputs[i];
                var inputRaw = bchTx.Inputs[i];
                if (input.Address == null)//not slp relevant input
                {
                    var inputTx = await _rpcClient.GetRawTransactionAsync(inputRaw.PrevOut.Hash);
                    input.BlockchainSatoshis = inputTx.Outputs[(int)inputRaw.PrevOut.N].Value.ToDecimal(MoneyUnit.Satoshi);
                    var pubKeys = inputTx.Outputs[(int)inputRaw.PrevOut.N].ScriptPubKey.GetDestinationPublicKeys();
                    //string address = null;
                    if (pubKeys.Length > 0)
                    {
                        var address = pubKeys.First().GetAddress(NBitcoin.ScriptPubKeyType.Legacy, _rpcClient.Network).ToString();
                        input.Address = new SlpAddress { Address = address, Id = -1 };
                    }
                    else
                    {
                        var address = inputTx.Outputs[(int)inputRaw.PrevOut.N].ScriptPubKey.GetDestinationAddress(_rpcClient.Network).ToString();
                        input.Address = new SlpAddress { Address = address, Id = -1 };
                    }
                    //input.Address = inputTx.Outputs[(int)inputRaw.PrevOut.N].ScriptPubKey.GetDestinationPublicKeys;
                }
                res.VIn[i] = new VIn
                {
                    TxId = input.SourceTxHash.ToHex(),
                    VOut = input.VOut,
                    N = i,
                    Sequence = inputRaw.Sequence.Value,
                    Value = Money.Satoshis(input.BlockchainSatoshis).ToDecimal(MoneyUnit.BTC),
                    CashAddress = input.Address.Address.ToPrefixedBchAddress(),
                    Addr = input.Address.Address.ToLegacyAddress(),
                    LegacyAddress = null,
                    ScriptSig = new ScriptSig
                    {
                        Asm = inputRaw.ScriptSig.ToString(),
                        Hex = inputRaw.ScriptSig.ToBytes().ToHex()
                    }
                };
            }
            return new TxDetails()
            {
                RetData = res,
                TokenInfo = tokenInfo
            };
        }

        public async Task<TxTokenDetails[]> GetTransactions(string tokenId, string address)
        {
            if (!address?.IsSlpAddress() ?? true)
                throw new Exception("Slp address must be passed as address argument");
            var slpPrefix = _rpcClient.GetSlpPrefix().ToString();

            var slpTxsOutputs = await _slpDb.SlpTransactionOutput
                           .Include(t => t.Address)
                           .Include(t => t.SlpTransaction)
                                .ThenInclude(t => t.SlpToken)
                           .Include(t => t.SlpTransaction)
                                .ThenInclude(t => t.SlpTransactionOutputs)
                                    .ThenInclude(t => t.Address)
                           .Where(t => t.SlpTransaction.SlpTokenId == tokenId.FromHex() && t.Address.Address == address)
                           .ToListAsync();

            var slpTxsInputs = await _slpDb.SlpTransactionInput
                           .Include(t => t.Address)
                           .Include(t => t.SlpTransaction)
                                .ThenInclude(t => t.SlpToken)
                           .Include(t => t.SlpTransaction)
                                .ThenInclude(t => t.SlpTransactionOutputs)
                                    .ThenInclude(t => t.Address)
                           .Where(t => t.SlpTransaction.SlpTokenId == tokenId.FromHex() && t.Address.Address == address)
                           .ToListAsync();

            var res = new List<TxTokenDetails>();
            foreach (var s in slpTxsOutputs)
            {
                if (res.Any(t => t.TxId == s.SlpTransaction.Hash.ToHex()))
                    continue;
                {
                    var txs = new TxTokenDetails
                    {
                        TxId = s.SlpTransaction.Hash.ToHex(),
                        TokenDetails = new TokenDetails
                        {
                            Detail = new Details
                            {
                                Decimals = s.SlpTransaction.SlpToken.Decimals,
                                DocumentSha256Hex = s.SlpTransaction.SlpToken.DocumentSha256Hex,
                                DocumentUri = s.SlpTransaction.SlpToken.DocumentUri,
                                Name = s.SlpTransaction.SlpToken.Name,
                                Symbol = s.SlpTransaction.SlpToken.Symbol,
                                TokenIdHEx = s.SlpTransaction.SlpToken.Hash.ToHex(),
                                TransactionType = s.SlpTransaction.Type.ToString(),
                                TxnBatonVOut = s.SlpTransaction.MintBatonVOut,
                                TxnContainsBaton = s.SlpTransaction.MintBatonVOut.HasValue,
                                VersionType = (int)s.SlpTransaction.SlpTokenType, //TODO: read from db
                                Outputs = s.SlpTransaction.SlpTransactionOutputs
                                    .Where(o => o.Address != null && o.Address.Address.StartsWith(slpPrefix))
                                    .Select(o => new SlpTxOutput
                                    {
                                        Address = o.Address?.Address ?? null,
                                        Amount = o.Amount.ToTokenValue(s.SlpTransaction.SlpToken.Decimals).ToString(CultureInfo.InvariantCulture)
                                    }).ToList()
                            },
                            Valid = s.SlpTransaction.State == SlpTransaction.TransactionState.SLP_VALID
                        }
                    };
                    res.Add(txs);
                }
            }

            foreach (var s in slpTxsInputs)
            {
                if (res.Any(t => t.TxId == s.SlpTransaction.Hash.ToHex()))
                    continue;
                {
                    var txs = new TxTokenDetails
                    {
                        TxId = s.SlpTransaction.Hash.ToHex(),
                        TokenDetails = new TokenDetails
                        {
                            Detail = new Details
                            {
                                Decimals = s.SlpTransaction.SlpToken.Decimals,
                                DocumentSha256Hex = s.SlpTransaction.SlpToken.DocumentSha256Hex,
                                DocumentUri = s.SlpTransaction.SlpToken.DocumentUri,
                                Name = s.SlpTransaction.SlpToken.Name,
                                Symbol = s.SlpTransaction.SlpToken.Symbol,
                                TokenIdHEx = s.SlpTransaction.SlpToken.Hash.ToHex(),
                                TransactionType = s.SlpTransaction.Type.ToString(),
                                TxnBatonVOut = s.SlpTransaction.MintBatonVOut,
                                TxnContainsBaton = s.SlpTransaction.MintBatonVOut.HasValue,
                                VersionType = (int)s.SlpTransaction.SlpTokenType, //TODO: read from db
                                Outputs = s.SlpTransaction.SlpTransactionOutputs
                                    .Where(o => o.Address != null && o.Address.Address.StartsWith(slpPrefix))
                                    .Select(o => new SlpTxOutput
                                    {
                                        Address = o.Address?.Address ?? null,
                                        Amount = o.Amount.ToTokenValue(s.SlpTransaction.SlpToken.Decimals).ToString(CultureInfo.InvariantCulture)
                                    }).ToList()
                            },
                            Valid = s.SlpTransaction.State == SlpTransaction.TransactionState.SLP_VALID
                        }
                    };
                    res.Add(txs);
                }
            }

            return res.ToArray();
        }

        public async Task<TxBurn[]> GetBurnTotal(string[] txIds)
        {
            if (txIds.Length > 100)
                throw new Exception("Max of 100 transactions limit reached");
            var txHashs = txIds.ToBytes();
            var slpTxs = await _slpDb.SlpTransaction
                           .Include(t => t.SlpToken)
                           .Include(t => t.SlpTransactionInputs)
                           .Include(t => t.SlpTransactionOutputs)
                           .Where(t => txHashs.Contains(t.Hash))
                           .Select(t => new { t.Hash, t.SlpToken.Decimals, t.SlpTransactionInputs, t.SlpTransactionOutputs })
                           .ToArrayAsync();

            var res = new List<TxBurn>();
            foreach (var slp in slpTxs)
            {
                var totalTokenInputs = slp.SlpTransactionInputs.Sum(i => i.SlpAmount).ToTokenValue(slp.Decimals);
                var totalTokenOutputs = slp.SlpTransactionOutputs.Sum(i => i.Amount).ToTokenValue(slp.Decimals);
                var burnTotal = totalTokenInputs - totalTokenOutputs;
                var burn = new TxBurn
                {
                    TransactionId = slp.Hash.ToHex(),
                    InputTotal = totalTokenInputs,
                    OutputTotal = totalTokenOutputs,
                    BurnTotal = burnTotal
                };
                res.Add(burn);
            }
            return res.ToArray();
        }
        #endregion
    }
}

