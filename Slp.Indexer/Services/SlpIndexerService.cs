using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using Slp.Common.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using EFCore.BulkExtensions;
using Slp.Common.Services;
using Slp.Common.DataAccess;
using Slp.Common.Models.DbModels;
using Slp.Common.Options;
using Slp.Common.Utility;
using Slp.Common.Models.Enums;
using Slp.Common.Extensions;
using PostgreSQLCopyHelper;
using Npgsql;
using System.Threading;

namespace Slp.Indexer.Services
{
    public class SlpIndexerService : IndexerServiceBase, IIndexerService
    {
        private readonly ISlpService _slpService;
        private readonly ISlpValidator _slpValidator;
        private readonly ISlpNotificationService _slpNotificationService;
        private readonly ISlpDbInitializer _slpDbInitializer;
        private readonly SlpDbContext _mainDb;
        private readonly DbContextOptions<SlpDbContext> _dbOptions;
        SD.DatabaseBackendType _databaseBackendType;
        //private readonly IBchRestClient _restClient;

        ConcurrentDictionary<string, SlpAddress> _usedAddresses = new ConcurrentDictionary<string, SlpAddress>();
        ConcurrentDictionary<string, SlpToken> _tokenMap = new ConcurrentDictionary<string, SlpToken>();
        ConcurrentDictionary<string, SlpTransaction> _slpTrCache = new ConcurrentDictionary<string, SlpTransaction>();
        //ConcurrentBag<string> _notSlpTxCache = new ConcurrentBag<string>();
        // ConcurrentDictionary<string, SlpTransaction> _cachedSlpTx = new ConcurrentDictionary<string, SlpTransaction>();
        Dictionary<int, string> _blockMap = new Dictionary<int, string>();
        ConcurrentQueue<Func<Task>> _zmqQueue = new ConcurrentQueue<Func<Task>>();
        private bool _zmqProcessingEnabled = true;
        //private int listenHeartBeatCounter = -1;
        #region PG_COPY
        PostgreSQLCopyHelper<SlpToken> tokenCopyHelper = new PostgreSQLCopyHelper<SlpToken>("public", nameof(SlpToken))
            .UsePostgresQuoting()
            .MapByteArray(nameof(SlpToken.Hash), x => x.Hash)
            .MapInteger(nameof(SlpToken.BlockHeight), x => x.BlockHeight)
            .MapInteger(nameof(SlpToken.Decimals), x => x.Decimals)
            .MapVarchar(nameof(SlpToken.DocumentSha256Hex), x => x.DocumentSha256Hex)
            .MapVarchar(nameof(SlpToken.ActiveMint), x => x.ActiveMint)
            .MapInteger(nameof(SlpToken.BlockLastActiveMint), x => x.BlockLastActiveMint)
            .MapInteger(nameof(SlpToken.BlockLastActiveSend), x => x.BlockLastActiveSend)
            .MapNumeric(nameof(SlpToken.CirculatingSupply), x => x.CirculatingSupply)
            .MapByteArray(nameof(SlpToken.DocumentUri), x => x.DocumentUri)
            .MapInteger(nameof(SlpToken.LastActiveSend), x => x.LastActiveSend)
            .MapVarchar(nameof(SlpToken.MintingBatonStatus), x => x.MintingBatonStatus)
            .MapVarchar(nameof(SlpToken.Name), x => x.Name)
            .MapInteger(nameof(SlpToken.SatoshisLockedUp), x => x.SatoshisLockedUp)
            .MapVarchar(nameof(SlpToken.Symbol), x => x.Symbol)
            .MapNumeric(nameof(SlpToken.TotalBurned), x => x.TotalBurned)
            .MapNumeric(nameof(SlpToken.TotalMinted), x => x.TotalMinted)
            .MapInteger(nameof(SlpToken.TxnsSinceGenesis), x => x.TxnsSinceGenesis)
            .MapInteger(nameof(SlpToken.ValidAddresses), x => x.ValidAddresses)
            .MapInteger(nameof(SlpToken.ValidTokenUtxos), x => x.ValidTokenUtxos)
            .MapInteger(nameof(SlpToken.VersionType), x => (int)x.VersionType);


        PostgreSQLCopyHelper<SlpBlock> blockCopyHelper = new PostgreSQLCopyHelper<SlpBlock>("public", nameof(SlpBlock))
            .UsePostgresQuoting()
            .MapInteger(nameof(SlpBlock.Height), x => x.Height)
            .MapByteArray(nameof(SlpBlock.Hash), x => x.Hash)
            .MapTimeStamp(nameof(SlpBlock.BlockTime), x => x.BlockTime)
            .MapBoolean(nameof(SlpBlock.IsSlp), x => x.IsSlp);

        PostgreSQLCopyHelper<SlpTransaction> txCopyHelper = new PostgreSQLCopyHelper<SlpTransaction>("public", nameof(SlpTransaction))
            .UsePostgresQuoting()
            .MapBigInt(nameof(SlpTransaction.Id), x => x.Id)
            .MapByteArray(nameof(SlpTransaction.Hash), x => x.Hash)
            .MapByteArray(nameof(SlpTransaction.SlpTokenId), x => x.SlpTokenId)
            .MapInteger(nameof(SlpTransaction.State), x => (int)x.State)
            .MapInteger(nameof(SlpTransaction.BlockHeight), x => x.BlockHeight)
            .MapVarchar(nameof(SlpTransaction.InvalidReason), x => x.InvalidReason)
            .MapInteger(nameof(SlpTransaction.MintBatonVOut), x => x.MintBatonVOut)
            .MapNumeric(nameof(SlpTransaction.AdditionalTokenQuantity), x => x.AdditionalTokenQuantity)
            .MapInteger(nameof(SlpTransaction.SlpTokenType), x => (int)x.SlpTokenType)
            .MapNumeric(nameof(SlpTransaction.TokenInputSum), x => x.TokenInputSum)
            .MapNumeric(nameof(SlpTransaction.TokenOutputSum), x => x.TokenOutputSum)
            .MapInteger(nameof(SlpTransaction.Type), x => (int)x.Type);

        PostgreSQLCopyHelper<SlpAddress> addressCopyHelper = new PostgreSQLCopyHelper<SlpAddress>("public", nameof(SlpAddress))
           .UsePostgresQuoting()
           .MapInteger(nameof(SlpAddress.Id), x => x.Id)
           .MapVarchar(nameof(SlpAddress.Address), x => x.Address); 
           //.MapInteger(nameof(SlpToken.BlockHeight), x => x.BlockHeight);

        PostgreSQLCopyHelper<SlpTransactionInput> inputCopyHelper = new PostgreSQLCopyHelper<SlpTransactionInput>("public", nameof(SlpTransactionInput))
            .UsePostgresQuoting()
            .MapBigInt(nameof(SlpTransactionInput.Id), x => x.Id)
            .MapBigInt(nameof(SlpTransactionInput.SlpTransactionId), x => x.SlpTransactionId)
            .MapInteger(nameof(SlpTransactionInput.AddressId), x => x.AddressId)
            .MapByteArray(nameof(SlpTransactionInput.SourceTxHash), x => x.SourceTxHash)
            .MapInteger(nameof(SlpTransactionInput.VOut), x => x.VOut)
            .MapNumeric(nameof(SlpTransactionInput.BlockchainSatoshis), x => x.BlockchainSatoshis)
            .MapNumeric(nameof(SlpTransactionInput.SlpAmount), x => x.SlpAmount)
            ;


        PostgreSQLCopyHelper<SlpTransactionOutput> outputCopyHelper = new PostgreSQLCopyHelper<SlpTransactionOutput>("public", nameof(SlpTransactionOutput))
            .UsePostgresQuoting()
            .MapBigInt(nameof(SlpTransactionOutput.Id), x => x.Id)
            .MapBigInt(nameof(SlpTransactionOutput.SlpTransactionId), x => x.SlpTransactionId)
            .MapInteger(nameof(SlpTransactionOutput.VOut), x => x.VOut)
            .MapInteger(nameof(SlpTransactionOutput.AddressId), x => x.AddressId)
            .MapNumeric(nameof(SlpTransactionOutput.Amount), x => x.Amount)
            .MapNumeric(nameof(SlpTransactionOutput.BlockchainSatoshis), x => x.BlockchainSatoshis)
            .MapBigInt(nameof(SlpTransactionOutput.NextInputId), x => x.NextInputId);
        
        #endregion
        public SlpIndexerService(
            RPCClient rpcClient,
            IConfiguration configuration,
            IHostApplicationLifetime hostApplicationLifetime,
            IServiceProvider serviceProvider,
            ILogger<IndexerServiceBase> log,
            //---------------------------------
            SlpDbContext db,
            DbContextOptions<SlpDbContext> dbOptions,
            ISlpService slpService,
            ISlpValidator slpValidator,
            ISlpNotificationService slpNotificationService,
            ISlpDbInitializer slpDbInitializer
            ) :
            base(rpcClient, configuration, hostApplicationLifetime, serviceProvider, log)
        {
            _slpDbInitializer = slpDbInitializer;
            _mainDb = db;
            _dbOptions = dbOptions;
            _slpService = slpService;
            _slpValidator = slpValidator;
            _slpValidator.RegisterTransactionProvider(TransactionProviderForSlpValidatorAsync);
            _slpNotificationService = slpNotificationService;
            _databaseBackendType = configuration.GetValue(nameof(SD.DatabaseBackend), SD.DatabaseBackend);           
        }

        SlpAddress GetAddress(string address)
        {
            if (_usedAddresses.TryGetValue(address, out var addr))
                return addr;
            return null;
        }
        int _addressIndex=0;
        SlpAddress GetOrCreateAddress(string address,int? blockHeight)
        {
            //using var db = new SlpDbContext(_dbOptions);
            if (!_usedAddresses.TryGetValue(address, out var addr))
            {
                var nextId = Interlocked.Increment(ref _addressIndex); // _usedAddresses.Any() ? _usedAddresses.Max(a => a.Value.Id) + 1 : 1;
                addr = new SlpAddress { Id = (int)nextId, Address = address, InDatabase = false };
                var res = _usedAddresses.TryAdd(address, addr);
                if (!res)
                {
                    if (!_usedAddresses.TryGetValue(address, out var addr2))
                        throw new Exception("Failed to retrieve address");
                    return addr2;
                }
            }
            return addr;
        }

        private Task<SlpTransaction> TransactionProviderForSlpValidatorAsync(string txId)
        {
            if (_slpTrCache.TryGetValue(txId, out SlpTransaction slpTransaction))
            {
                if (slpTransaction == null)
                    throw new NullReferenceException($"Slp tx cache contains null reference for tx {txId}");
                return Task.FromResult(slpTransaction);
            }
            return Task.FromResult<SlpTransaction>(null);
            //if (_notSlpTxCache.Contains(txId))
            //    return null;

            ////this is fallback to read transaction from db in case cache is missing record
            //// transaction must not be cached here - this will only provide data when normal processing has not yet hit this transactions
            //var rawTx = await _rpcClient.GetRawTransactionAsync(new uint256(txId));
            //if (rawTx == null)
            //{
            //    try { _notSlpTxCache.Add(txId); } catch { }
            //    return null;
            //}
            //slpTransaction = _slpService.GetSlpTransaction(rawTx);
            //if (slpTransaction == null)
            //{
            //    try { _notSlpTxCache.Add(txId); } catch { }
            //    return null;
            //}
            //return slpTransaction;
        }
        public async Task CommitBatchToDb(
            int batchCounter,
            List<SlpTransaction> localBatch,
            List<SlpBlock> localBlocks)
        {
            _log.LogInformation("Saving batch {0} of {1} blocks and {2} txs to db async...", batchCounter,localBlocks.Count, localBatch.Count);                
            var tokens = localBatch.Where(t => t.SlpToken != null && t.Type == SlpTransactionType.GENESIS).Select(t => (t.SlpToken,t.BlockHeight)).ToList();
            tokens.ForEach(t =>
            {
                t.SlpToken.BlockHeight = t.BlockHeight;
                t.SlpToken.Name = t.SlpToken.Name.Trim().Trim('\0');
                t.SlpToken.DocumentSha256Hex = t.SlpToken.Name.Trim().Trim('\0');
                _tokenMap.AddOrReplace(t.SlpToken.Hash.ToHex(), t.SlpToken);
             });

            var outputs = new List<SlpTransactionOutput>();
            var inputs = new List<SlpTransactionInput>();
            var newAddresses = new Dictionary<string, SlpAddress>();
            _log.LogInformation("Collecting inputs and outputs...");
            foreach (var tr in localBatch)
            {
                outputs.AddRange(tr.SlpTransactionOutputs);               
                //foreach (var o in tr.SlpTransactionOutputs)
                //{
                //    //outputs.Add(o);                        
                //    //var addr = GetOrCreateAddress(o.Address.Address, tr.BlockHeight);
                //    //o.Address = addr;
                //    //o.AddressId = addr.Id;
                //    if (!newAddresses.ContainsKey(o.Address.Address) && addr.InDatabase==false)
                //    {
                //        newAddresses.Add(o.Address.Address, addr);
                //        addr.InDatabase = true;
                //    }
                //}
                //foreach (var i in tr.SlpTransactionInputs)
                //    inputs.Add(i);
                inputs.AddRange(tr.SlpTransactionInputs);
                //Console.Write(".");
                //_log.LogInformation(tr.Hash.ToHex());
                //_slpTrCache.AddOrReplace(tr.Hash.ToHex(), tr);
            }
            _log.LogInformation("Checking for new addresses...");
            foreach (var o in outputs)
            {
                if (_usedAddresses.TryGetValue(o.Address.Address, out var value) && !value.InDatabase)
                    if (!newAddresses.ContainsKey(o.Address.Address))
                    {
                        newAddresses.Add(o.Address.Address, value);
                        value.InDatabase = true;
                    }
            }

            //_log.LogInformation("Setting address ids...");
            //outputs.ForEach(o => { o.AddressId = o.Address.Id;});
            
            {
                using var db = new SlpDbContext(_dbOptions);
                //fast relink based on local _slpTrCache and then bulk insert them - this is possible since we are 
                // settting id on client side
                _log.LogInformation("Processing inputs...", batchCounter);
                var outputsToUpdate = new List<SlpTransactionOutput>();
                foreach (var input in inputs)
                {
                    var outputSlpTx = _slpTrCache.TryGet(input.SourceTxHash.ToHex());
                    //pointing to non slp transaction - not relevant at the moment - to set bch data we need to gather data from bch indexer
                    if (outputSlpTx == null)
                    {
                        continue;
                    }
                    //throw new Exception($"Invalid output tx reference {input.SlpSourceTransactionHex}! Make sure slpTrCache is valid!");
                    var referencedOutput = outputSlpTx.SlpTransactionOutputs.ElementAt(input.VOut);
                    var outputAddress = GetAddress(referencedOutput.Address.Address);
                    if (outputAddress == null)
                    {
                        _log.LogWarning("Input does not have valid slp output");
                        continue; //
                    }
                    if (outputAddress.InDatabase == false)
                    {
                        newAddresses.Add(outputAddress.Address, outputAddress);
                        outputAddress.InDatabase = true;
                        //throw new Exception($"Cannot find input address from ref output {outputAddress.Address} at tx {input.SourceTxHash.ToHex()}:{input.VOut}");
                    }

                    input.Address = outputAddress;
                    input.AddressId = outputAddress.Id;
                    input.SlpAmount = referencedOutput.Amount;
                    input.BlockchainSatoshis = referencedOutput.BlockchainSatoshis;

                    if (!outputSlpTx.TokenInputSum.HasValue)
                        outputSlpTx.TokenInputSum = 0;
                    outputSlpTx.TokenInputSum += input.SlpAmount; //accumullate to transaction level for faster query

                    input.SlpTransaction.TokenInputSum += input.SlpAmount;
                    referencedOutput.NextInput = input;
                    referencedOutput.NextInputId = input.Id;

                    if (!localBatch.Contains(outputSlpTx))
                        outputsToUpdate.Add(referencedOutput);
                }
                _log.LogInformation("Update slp state...");
                //before anything is changed in database make sure that counter is set to first block
                //this would be probaby better 
                if (localBlocks.Any())
                    await db.UpdateSlpStateAsync(localBlocks.First().Height, localBlocks.First().Hash.ToHex());

                if (_databaseBackendType == SD.DatabaseBackendType.POSTGRESQL) //bulk extensions does not support postgre backend yet so we use postgrebulkcopy to insert
                {                    
                    try
                    {
                        var dgConnection = db.Database.GetDbConnection();
                        if (!(dgConnection is Npgsql.NpgsqlConnection pgConnection))
                            throw new Exception("Only postgre sql is currently imported!");
                        _log.LogInformation("Storing to db...");
                        if (pgConnection.State != System.Data.ConnectionState.Open)
                            pgConnection.Open();
                        

                        var count = await blockCopyHelper.SaveAllAsync(pgConnection, localBlocks);
                        _log.LogInformation("Written {0} blocks.", count);

                        count = await tokenCopyHelper.SaveAllAsync(pgConnection, tokens.Select(t=>t.SlpToken));
                        _log.LogInformation("Written {0} new tokens.", count);

                        count = await txCopyHelper.SaveAllAsync(pgConnection, localBatch);
                        _log.LogInformation("Written {0} txs.", count);

                        count = await addressCopyHelper.SaveAllAsync(pgConnection, newAddresses.Values);
                        _log.LogInformation("Written {0} new addresses.", count);

                        count = await inputCopyHelper.SaveAllAsync(pgConnection, inputs);
                        _log.LogInformation("Written {0} new inputs.", count);
                        
                        count = await outputCopyHelper.SaveAllAsync(pgConnection, outputs);
                        _log.LogInformation("Written {0} new outputs.", count);
                    }
                    catch (Exception e)
                    {
                        throw;
                    }

                }
                else if (_databaseBackendType == SD.DatabaseBackendType.MSSQL)
                {
                    //first delete all remaining blocks
                    //token is never removed from database at least not in the catchup/sync phase so all operations should be upsert
                    await db.BulkInsertOrUpdateAsync(tokens.Select(t => t.SlpToken).ToList());
                    await db.BulkInsertAsync(localBatch);
                    await db.BulkInsertAsync(newAddresses.Values.ToList());
                    await db.BulkInsertAsync(outputs);
                    await db.BulkInsertAsync(inputs);
                    await db.BulkInsertAsync(localBlocks);
                    // these output are not part of local batch and we need to update them
                    // with separate command
                    await db.BulkUpdateAsync(outputsToUpdate);
                }                
            }
            _log.LogInformation("Batch {0} saved to database:  {1} slp transactions", batchCounter++, localBatch.Count);
        }
        public async Task SyncCurrentMempoolAsync()
        {
            _log.LogInformation("Clearing all mempool transactions from db...");
            await ResetUnconfirmedTransactionsAsync();

            _log.LogInformation("Reading raw transactions from mempool...");
            var currentBchMempoolArray = await _rpcClient.GetRawMempoolAsync();
            // Perform a toposort on current bch mempool.
            var mempoolSlpTxs = new Dictionary<string, SlpTransaction>();
            foreach (var txid in currentBchMempoolArray)
            {
                var rawTransaction = await _rpcClient.GetRawTransactionAsync(txid);
                var slpTransaction = _slpService.GetSlpTransaction(rawTransaction);
                if (slpTransaction != null)
                {
                    mempoolSlpTxs.Add(txid.ToString(), slpTransaction);
                }
                //else
                //{
                //    //try { _notSlpTxCache.Add(txid.ToString()); } catch { }
                //}
            }
            List<string> sortedStack = new List<string>();
            TopologicalSort(mempoolSlpTxs, sortedStack);
            if (sortedStack.Count != mempoolSlpTxs.Count)
                throw new Exception("Transaction count is incorrect after topological sorting.");

            _log.LogInformation("Storing mempool trasactions to db...");
            foreach (var tr in mempoolSlpTxs)
            {
                try
                {
                    sortedStack.RemoveAt(0);
                    await SyncMempoolTransactionAsync(tr.Value,false);
                    //using var db = new SlpDbContext(_dbOptions);
                    //await AddMempoolTransactionAsync(db, tr.Value);
                    //await db.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _log.LogError("Mempool sync failed with: " + e.Message);
                    throw;
                }
            }
            // await db.SaveChangesAsync();
            _log.LogInformation("Mempool transactions stored to db.");
            _log.LogInformation("BCH mempool txn count: {0}", (await _rpcClient.GetRawMempoolAsync()).Length);
            _log.LogInformation("SLP mempool txn count: {0}", mempoolSlpTxs.Count);
        }

        public async Task ProcessQueue()
        {
            _log.LogInformation("ZMQ processing enabled: " + _zmqProcessingEnabled);
            while (true)
            {
                _listenHeartBeatCounter++;
                if (!_zmqQueue.Any() || !_zmqProcessingEnabled)
                {
                    await Task.Delay(SD.HeartBeatInMiliseconds);
                    continue;
                }                

                if (!_zmqQueue.TryDequeue(out Func<Task> func))
                {
                    await Task.Delay(250);
                    continue;
                }
                await func();
            }
        }

        public void ListenToZmqAsync()
        {
            _slpNotificationService.OnNewBlock += (Block block) =>
            {
                _zmqQueue.Enqueue(() =>
                {
                    if (block == null)
                        return Task.Delay(10);
                    var blockHash = block.GetHash();
                    Console.WriteLine();
                    _log.LogInformation("New block found: {0}", blockHash);
                    return SyncMempoolBlockAsync(block);
                });
            };
            _slpNotificationService.OnNewTransaction += (Transaction transaction) =>
            {
                _zmqQueue.Enqueue(() =>
                { 
                    if (transaction == null)
                        return Task.Delay(10);

                    var slpTr = _slpService.GetSlpTransaction(transaction);
                    if (slpTr == null)
                    {
                        //mark this transaction as non slp
                        //try { _notSlpTxCache.Add(transaction.GetHash().ToString()); } catch { }
                        //check non-slp transaction for slp burns
                        slpTr = CheckNonSlpTransactionForSlpBurn(transaction, null);
                        if (slpTr == null)
                        {
                            Console.Write("I");
                            _log.LogDebug("Transaction {0} is not SLP ignored.", transaction.GetHash());
                            return Task.CompletedTask;
                        }
                        else
                        {
                            Console.WriteLine();
                            _log.LogInformation("Transaction {0} found slp burn inputs.", transaction.GetHash());
                        }
                    }
                    Console.WriteLine();
                    return SyncMempoolTransactionAsync(slpTr,true);
                }
                );
            };
        }

        public async Task ResetUnconfirmedTransactionsAsync(int commandsTimeout = SD.TimeConsumingQueryTimeoutSeconds)
        {
            using var db = new SlpDbContext(_dbOptions);
            db.Database.SetCommandTimeout(commandsTimeout);
            var txs = await db.SlpTransaction
                //.AsNoTracking()
                .Where(t => !t.BlockHeight.HasValue == true)
                .ToArrayAsync();
            if (txs.Any())
            {
                _log.LogInformation("Deleting all unconfirmed output nextId links...");
                var outputsClearLinks = db.SlpTransactionOutput
                                            //.AsNoTracking()
                                            .Include(t => t.NextInput)
                                                .ThenInclude(t => t.SlpTransaction)
                                            .Where(t => t.NextInputId != null && !t.NextInput.SlpTransaction.BlockHeight.HasValue);
                var updated = await outputsClearLinks.BatchUpdateAsync(a => new SlpTransactionOutput { NextInputId = null });

                _log.LogInformation("Deleting all unconfirmed inputs...");
                var lastBlockTxInputs = db.SlpTransactionInput
                    //.AsNoTracking()
                    .Include(t => t.SlpTransaction)
                    .Where(t => !t.SlpTransaction.BlockHeight.HasValue);
                await lastBlockTxInputs.BatchDeleteAsync();

                _log.LogInformation("Deleting all unconfirmed outputs...");
                var lastBlockTxOutputs = db.SlpTransactionOutput
                    //.AsNoTracking()
                    .Include(t => t.SlpTransaction).Where(t => !t.SlpTransaction.BlockHeight.HasValue);
                await lastBlockTxOutputs.BatchDeleteAsync();

                _log.LogInformation("Deleting all unconfirmed transactions...");
                var lastBlockTxs = db.SlpTransaction
                    //.AsNoTracking()
                    .Where(t => !t.BlockHeight.HasValue);
                await lastBlockTxs.BatchDeleteAsync();

                _log.LogInformation("Deleting all slp tokens with zero transactions...");
                
             
                //remove tx also from cache and validator
                foreach (var tx in txs)
                {
                    _slpTrCache.TryRemove(tx.Hash.ToHex(), out SlpTransaction slpTx);
                    _slpValidator.RemoveTransactionFromValidation(tx.Hash.ToHex());
                }
            }
            //_slpMempool.tryremo
        }


        public class TrState 
        {
            public bool IsSlp { get; set; }
            public bool Added { get; set; }
        }

      
 private async Task LoadSlpTransactionsCacheFromDb()
        {
            _log.LogInformation("Caching SLP transactions... ( might take a while, also make sure you have enough RAM on your machine )");
            _log.LogInformation("Caching tokens...");
            var tokens = await _mainDb.SlpToken
                .AsNoTracking()
                .ToDictionaryAsync(k => k.Hash.ToHex());

            //load all components fast
            _log.LogInformation("[Parallel]Reading all slp transactions from db...");
            var task0 = _mainDb.SlpTransaction
                .AsNoTracking()
                .ToDictionaryAsync(t => t.Id);

            Dictionary<long, SlpTransactionInput> slpTxIns = new Dictionary<long, SlpTransactionInput>();
            var task1 = Task.Run(() =>
            {
                using var dbInput = new SlpDbContext(_dbOptions);
                _log.LogInformation("[Parallel] Reading all slp transactions inputs from db...");
                slpTxIns = dbInput.SlpTransactionInput
                    .AsNoTracking()
                    .ToDictionary(t => t.Id);
            });
            Dictionary<long, SlpTransactionOutput> slpTxOuts = new Dictionary<long, SlpTransactionOutput>();
            var task2 = Task.Run(() =>
            {
                using var dbOutput = new SlpDbContext(_dbOptions);
                _log.LogInformation("[Parallel] Reading all slp transactions outputs from db...");
                slpTxOuts = dbOutput.SlpTransactionOutput
                    .AsNoTracking()
                    .ToDictionary(t => t.Id);
            });
            
            var allAddresses = new Dictionary<int, SlpAddress>();
            var task3 = Task.Run(() =>
            {
                using var dbOutput = new SlpDbContext(_dbOptions);
                _log.LogInformation("[Parallel] Reading all used addresses from db...");
                allAddresses = dbOutput.SlpAddress
                            .AsNoTracking()
                            .ToDictionary(t => t.Id);
            });
            await Task.WhenAll(task0, task1, task2, task3);

            _log.LogInformation("Adding tokens to cache...");
            foreach (var t in tokens)
                _tokenMap.TryAdd(t.Key, t.Value);

            var slpTxs = task0.Result;
            foreach (var tr in slpTxs)
            {
                if (tr.Value.Type == SlpTransactionType.BURN && tr.Value.SlpTokenId == null) //burn transactions does not have slp token references
                    continue;
                var token = _tokenMap.TryGet(tr.Value.SlpTokenId.ToHex());
                tr.Value.SlpToken = token;
            }

            //connect keys locally - much faster than executing single query with joins
            _log.LogInformation("Adding addresses to cache...");
            foreach (var a in allAddresses)
            {
                _usedAddresses.TryAdd(a.Value.Address, a.Value);
                a.Value.InDatabase = true;
            }
            _addressIndex = _usedAddresses.Any() ?  _usedAddresses.Max(a => a.Value.Id) : 0;

            //var addr = GetAddress("simpleledger:qpnemhrgtwnegp0z5d5lm6fqvnj3cpv5jg45rrslgk");

            _log.LogInformation("Adding transaction inputs to transactions...");
            foreach (var i in slpTxIns)
            {
                var tx = slpTxs.TryGet(i.Value.SlpTransactionId);
                i.Value.SlpTransaction = tx;

                if (i.Value.AddressId.HasValue && allAddresses.TryGetValue(i.Value.AddressId.Value, out var addr))
                    i.Value.Address = addr;                                
                
                tx.SlpTransactionInputs.Add(i.Value);
            }
            _log.LogInformation("Adding transaction outpus to transactions...");
            foreach (var o in slpTxOuts)
            {
                var tx = slpTxs.TryGet(o.Value.SlpTransactionId);
                o.Value.SlpTransaction = tx;

                if (allAddresses.TryGetValue(o.Value.AddressId, out var addr))
                    o.Value.Address = addr;
                //o.Value.Address = allAddresses.TryGet(o.Value.AddressId);
                tx.SlpTransactionOutputs.Add(o.Value);
            }
            _log.LogInformation("Preparing slp transaction cache...");
            foreach (var i in slpTxs)
                _slpTrCache.AddOrReplace(i.Value.Hash.ToHex(), i.Value);
        }
        public async Task SyncMempoolTransactionAsync(SlpTransaction slpTr, bool notify)
        {
            using var db = new SlpDbContext(_dbOptions);
            var added = await AddMempoolTransactionAsync(db, slpTr);
            await db.SaveChangesAsync();
            if(notify)
                _slpNotificationService.NotifySlpTransaction(slpTr);
        }

        private async Task<bool> AddMempoolTransactionAsync(SlpDbContext db, SlpTransaction slpTr)
        {
            try
            {
                // using var db = new SlpDbContext(_dbOptions);
                //we are storing new value here so we need separate context so main context is not tracking values
                if (slpTr == null)
                    throw new ArgumentNullException(nameof(slpTr));
                var existing = await db.SlpTransaction.FirstOrDefaultAsync(tr => tr.Hash == slpTr.Hash);
                if (existing != null)
                {
                    //this will ignore if transaction was handled on transaction zmq message
                    _log.LogInformation("Mempool item already exists in database: {0}", slpTr.Hash.ToHex());
                    return false;
                }
                else
                {
                    if (
                         (slpTr.Type == SlpTransactionType.MINT || slpTr.Type == SlpTransactionType.SEND) && 
                         !_tokenMap.ContainsKey(slpTr.SlpTokenId.ToHex())
                         )
                    {
                        _log.LogWarning("Slp tx {0}, {1} does not have valid token reference {2}",
                            slpTr.Hash.ToHex(), slpTr.Type.ToString(), slpTr.SlpTokenId.ToHex());
                        return false; //invalid transaction - do not process it since it has invalid token reference
                    }
                    //Console.WriteLine();
                    //_log.LogInformation("Saving new mempool tx {0}...", slpTr.Hex);
                    //!!! add this before validation starts so data is properly fetched
                    if (slpTr.Type == SlpTransactionType.GENESIS && !_tokenMap.ContainsKey(slpTr.Hash.ToHex()))
                        _tokenMap.AddOrReplace(slpTr.Hash.ToHex(), slpTr.SlpToken);
                    _slpTrCache.AddOrReplace(slpTr.Hash.ToHex(), slpTr);

                    //slpTr.Unconfirmed = true;
                    slpTr.BlockHeight = null;

                    db.DbSlpIdManager = _mainDb.DbSlpIdManager;
                    db.AssignTransactionsNewId(new SlpTransaction[] { slpTr }, null);

                    //connect inputs with previous outputs
                    foreach (var input in slpTr.SlpTransactionInputs)
                    {
                        var sourceTr = _slpTrCache.TryGet(input.SourceTxHash.ToHex());
                        if (sourceTr == null)
                        {
                            var addr = GetOrCreateAddress("NON-SLP", slpTr.BlockHeight);
                            if (!addr.InDatabase)
                            {
                                await db.SlpAddress.AddAsync(addr);
                                addr.InDatabase = true;
                            }
                            input.Address = addr;
                            input.AddressId = addr.Id;
                            continue; //non - slp transaction
                        }

                        SlpTransaction inputSourceTransaction = null;
                        try
                        {
                            if(!_slpTrCache.TryGetValue(input.SourceTxHash.ToHex(), out inputSourceTransaction) )
                                throw new Exception($"Failed to retrieve input source transaction {input.SourceTxHash.ToHex()}");
                            var output = inputSourceTransaction.SlpTransactionOutputs.ElementAtOrDefault(input.VOut);
                            if( output == null )
                                throw new Exception($"Failed to retrieve input source transaction {input.SourceTxHash.ToHex()} output at {input.VOut}");
                            input.Address = output.Address;
                            input.AddressId = output.AddressId;
                            input.SlpAmount = output.Amount;
                            input.BlockchainSatoshis = output.BlockchainSatoshis;

                            if (!slpTr.TokenInputSum.HasValue)
                                slpTr.TokenInputSum = 0;
                            slpTr.TokenInputSum += input.SlpAmount;
                            //link from output to input where output was spent ( if spent )
                            output.NextInput = input;
                            output.NextInputId = input.Id;
                            db.Entry(output).State = EntityState.Modified;
                        }
                        catch (Exception e)
                        {
                            _log.LogError("AddMempoolTransactionAsync failed with: " + e.Message);
                            throw;
                        }
                    }
                    //add token out sum to tr level
                    slpTr.TokenOutputSum = slpTr.SlpTransactionOutputs.Sum(o => o.Amount);

                    //add slp transaction to local cache so validator will see it
                    if (slpTr.Type != SlpTransactionType.BURN)
                    {
                        var validationResult = await _slpValidator.IsValidAsync(slpTr.Hash.ToHex(), slpTr.SlpTokenId.ToHex());
                        if (validationResult.Item1)
                            slpTr.State = SlpTransaction.TransactionState.SLP_VALID;
                        else
                        {
                            slpTr.State = SlpTransaction.TransactionState.SLP_INVALID;
                            slpTr.InvalidReason = validationResult.Item2.Truncate(SD.InvalidReasonLength);
                            _log.LogWarning("Mempool transaction {0} is not valid: {1}", slpTr.Hash.ToHex(), slpTr.InvalidReason);
                        }
                    }

                    if (slpTr.Type == SlpTransactionType.MINT || slpTr.Type == SlpTransactionType.SEND)
                    {
                        if (slpTr.SlpTokenId == null )
                            throw new Exception($"Got transaction that does not have genesis transaction(token) present in database {slpTr.Hash.ToHex()}");
                    }
                    else if (slpTr.Type == SlpTransactionType.GENESIS)
                    {
                        if (slpTr.SlpToken == null)
                            throw new Exception($"Genesis transaction {slpTr.Hash.ToHex()} does not hold token data!");

                        var token = await db.SlpToken.FindAsync(slpTr.SlpTokenId);
                        if( token == null)
                            db.Entry(slpTr.SlpToken).State = EntityState.Added;
                        //if (token != null)
                        //    slpTr.SlpToken = token; //use from db if already exist                        
                        //else
                        //    db.Entry(slpTr.SlpToken).State = EntityState.Added;
                    }
                    // else //burn transaction - no token reference here

                    db.Entry(slpTr).State = EntityState.Added;
                    foreach (var i in slpTr.SlpTransactionInputs)
                    {
                        i.Address = GetOrCreateAddress(i.Address.Address, null);
                        if (!i.Address.InDatabase)
                        {
                            await db.SlpAddress.AddAsync(i.Address);
                            i.Address.InDatabase = true;
                        }
                        //db.Entry(i.Address).State = EntityState.Unchanged;
                        db.Entry(i).State = EntityState.Added;
                    }
                    foreach (var o in slpTr.SlpTransactionOutputs)
                    {
                        o.Address = GetOrCreateAddress(o.Address.Address, null);
                        if (!o.Address.InDatabase)
                        {
                            await db.SlpAddress.AddAsync(o.Address);
                            o.Address.InDatabase = true;
                        }
                        o.AddressId = o.Address.Id;
                        //db.Entry(o.Address).State = EntityState.Unchanged;
                        db.Entry(o).State = EntityState.Added;
                    }
                    _log.LogInformation("Mempool {0} transaction {1} added.", slpTr.Type, slpTr.Hash.ToHex());
                    return true;
                }
            }
            catch (Exception e)
            {
                _log.LogError("Mempool sync ERR: {0} {1}", e.Message, slpTr.Hash.ToHex());
                _log.LogError(e.StackTrace);
                throw;
            }
        }

        public async Task SyncMempoolBlockAsync(Block block)
        {
            if (await _mainDb.SlpBlock.AnyAsync(b => b.Hash == block.GetHash().ToBytes(false)))
            {
                _log.LogWarning("Block {0},{1} already handled.", block.GetHash(), block.GetCoinbaseHeight().Value);
                return; //block already handled
            }
            //prevent zmq processing when merging block data - if there is new transaction added to db that is also in the current block we can have 
            _zmqProcessingEnabled = false;

            var reorgCheckResult = await CheckForReorgAsync(block);
            //delete all mempool transactions - if they were included in block they will be readded on sync
            _log.LogInformation($"{block.GetHash()}: Clearing all mempool transactions from db before catchup...");
            await ResetUnconfirmedTransactionsAsync();

            var fetchedBlocks = new ConcurrentDictionary<int, Tuple<Block, List<SlpTransaction>, List<Transaction>>>();
            //catchupAsync will process last block transaction and add any missing transactions
            //that were not broadcasted, normally this will do nothi
            _log.LogInformation($"{block.GetHash()}: Read next block(s)...");
            var task = ReadBlocksFromHeightAsync(reorgCheckResult.Height, fetchedBlocks, 1, 1);
            await CatchupAsync(reorgCheckResult.Height, 100,1, fetchedBlocks);
            await task;
            
            _log.LogInformation($"{block.GetHash()}: Resyncing current mempool transactions...");
            await SyncCurrentMempoolAsync();

            _log.LogInformation($"{block.GetHash()}: Publishing new slp block processed...");
            var lastBlock = await _mainDb.SlpBlock.FirstOrDefaultAsync(b => b.Hash == block.GetHash().ToBytes(false));
            _slpNotificationService.NotifySlpBlock(lastBlock);
            _zmqProcessingEnabled = true;
        }

        async Task ReadBlocksFromHeightAsync(
             int start,
             ConcurrentDictionary<int, Tuple<Block, List<SlpTransaction>, List<Transaction>>> fetchedBlockSlpTx,
             int limitFetchSize,
             int workerSize)
        {
            var startHeight = start;
            _log.LogDebug($"Retrieving last block count using first worker...");
            var end = await _rpcClient.GetBlockCountAsync();
            List<Task> tasks = new List<Task>();
            var workers = new List<RPCClient>();
            for (var i = 0; i < workerSize; i++)
                workers.Add(_serviceProvider.GetService<RPCClient>());
            //var halfLimit = limitFetchSize / 2;
            for (int height = startHeight; height <= end; height++)
            {
                _log.LogDebug($"Reading block {height}...");
                if (height >= end) //right before end make sure we fix upper bound so we process all blocks
                {
                    var newEnd = await _rpcClient.GetBlockCountAsync();
                    _log.LogDebug($"Fixing end from {end} to {newEnd} height.");
                    end = newEnd;
                }
                //to prevent out of memory set block limit when this thread stops reading
                //bool limitReached = false;
                while (fetchedBlockSlpTx.Count > limitFetchSize) //limit reached -> wait for processing to reach half limit
                {
                    //limitReached = true;
                    await Task.Delay(1000);
                    Console.Write("L");
                }
                tasks.Add(FetchBlockAsync(workers[tasks.Count], height, fetchedBlockSlpTx));
                if (tasks.Count >= workerSize) //if there too many tasks sleep a little :)
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
            }
        }

        async Task FetchBlockAsync(RPCClient rpcClient, int h,
            ConcurrentDictionary<int, Tuple<Block, List<SlpTransaction>, List<Transaction>>> fetchedBlockSlpTx
            )
        {
restart:
            try
            {

                if (fetchedBlockSlpTx.TryGetValue(h, out var value) && value != null)
                    return; //already have it;

                var heightLocal = h;
                var block = await rpcClient.GetBlockAsync(heightLocal);
                List<SlpTransaction> slpTxs = new List<SlpTransaction>();
                List<Transaction> txs = new List<Transaction>();
                foreach (var tr in block.Transactions)
                {
                    var slpTr = _slpService.GetSlpTransaction(tr);
                    if (slpTr != null)
                    {
                        foreach (var o in slpTr.SlpTransactionOutputs)
                        {
                            var address = GetOrCreateAddress(o.Address.Address, h);
                            o.AddressId = address.Id;
                        }
                        slpTxs.Add(slpTr);
                        _slpTrCache.AddOrReplace(slpTr.Hash.ToHex(), slpTr);
                        if (slpTr.Type == SlpTransactionType.GENESIS && !_tokenMap.ContainsKey(slpTr.Hash.ToHex()))
                            _tokenMap.TryAdd(slpTr.Hash.ToHex(), slpTr.SlpToken);
                    }
                    else
                    {
                        txs.Add(tr);
                    }
                }


                while (!fetchedBlockSlpTx.ContainsKey(h)) //ensure height is added. If another worker added then just skipp this step
                    fetchedBlockSlpTx.TryAdd(h, new Tuple<Block, List<SlpTransaction>, List<Transaction>>(block, slpTxs, txs));
            }
            catch (Exception e)
            {
                _log.LogWarning($"Failed to fetch block {h}: {e.Message}. Retrying...");
                goto restart;
            }
        }

        public async Task SyncBlocksAsync()
        {
            //_log.LogInformation("Retrieving current node block height...");
            //var currentChainHeight = await _rpcClient.GetBlockCountAsync();                              
            _log.LogInformation("Processing blocks...");
            int currentBlockHeight = await _mainDb.GetLastSavedCheckpointAsync();
            var rpcWorkerCount = _configuration.GetValue(nameof(SD.RPCWorkerCount), SD.RPCWorkerCount);
            var rpcBlockPrefetchLimit = _configuration.GetValue(nameof(SD.RpcBlockPrefetchLimit), SD.RPCWorkerCount);
            var batchSize = _configuration.GetValue(nameof(SD.DbCommitBatchSize), SD.DbCommitBatchSize);

            var fetchedBlocks = new ConcurrentDictionary<int, Tuple<Block, List<SlpTransaction>, List<Transaction>>>();
            //1. this task will fill fetch blocks ahead to pipeline - non blocking
            var task = ReadBlocksFromHeightAsync(currentBlockHeight, fetchedBlocks,rpcBlockPrefetchLimit, rpcWorkerCount);
            //2. this task will process fetched blocks for slp transactions and catch up latest block
            await CatchupAsync(currentBlockHeight, batchSize, rpcWorkerCount, fetchedBlocks);
            //ensure that also task 1 is finished
            await task;
        }

        private async Task CatchupAsync(
          int currentBlockHeight,
          int batchsize,
          int workerSize,
          ConcurrentDictionary<int, Tuple<Block, List<SlpTransaction>, List<Transaction>>> fetchedBlocks
          )
        {
            var batchBlocks = new ConcurrentQueue<SlpBlock>();
            var batchToSaveToDb = new ConcurrentQueue<SlpTransaction>();
            int height = currentBlockHeight;

            Task saveToDbTask = null;
            int batchCounter = 0;
            var maxHeight = await _rpcClient.GetBlockCountAsync();
            for (; height <= maxHeight; height++) //there is a 
            {
                var start = DateTime.Now;
                if (height == maxHeight - 1)
                {
                    //there is a small window of error that block will be produced after this call resulting in non-synced 
                    //TODO: handle also this scenari so we do not wait for next block  to throw exception
                    maxHeight = await _rpcClient.GetBlockCountAsync();
                }
                var res = false;
                Tuple<Block, List<SlpTransaction>, List<Transaction>> data = null;
                res = fetchedBlocks.TryGetValue(height, out data);
                while (!res || data == null) //block fetch thread will fill this dictionary that our main thread will fetch for data
                {
                    //wait for batch to finish
                    while(fetchedBlocks.Count < workerSize && height + fetchedBlocks.Count < maxHeight)
                        await Task.Delay(10);
                    res = fetchedBlocks.TryGetValue(height, out data);
                    //if (res && data != null)
                    //    break;
                    //await FetchBlockAsync(_rpcClient, height, fetchedBlocks);
                    //res = fetchedBlocks.TryGetValue(height, out data);
                }
                if (!fetchedBlocks.TryRemove(height, out data))
                    throw new Exception("Failed to remove fetched block!");
                currentBlockHeight = height;
                var currentSlpBlock = new SlpBlock()
                {
                    Hash = data.Item1.GetHash().ToBytes(false),
                    Height = currentBlockHeight,
                    BlockTime = data.Item1.Header.BlockTime.UtcDateTime
                };
                var blockHash = currentSlpBlock.Hash.ToHex();
                batchBlocks.Enqueue(currentSlpBlock);

                Stat stat = new Stat();
                var slpTransactions = await ProcessBlock(data, stat);
                if (slpTransactions != null && slpTransactions.Any())
                {
                    _mainDb.AssignTransactionsNewId(slpTransactions.Values, height);
                    currentSlpBlock.IsSlp = true;
                    //_blockMap.AddOrReplace(height, currentBlockHash + "#1");
                    // add to save queue
                    foreach (var tr in slpTransactions.Values)
                        batchToSaveToDb.Enqueue(tr);
                }
                if (slpTransactions.Any())
                {
                    var dur = DateTime.Now - start;
                    Console.WriteLine();
                    _log.LogInformation("Block {0}: {1} SLP of total {2} txs, PreFetch {3}, CurrentBatch: {4}, ({5}ms)",
                        height,
                        slpTransactions.Count,
                        data.Item1.Transactions.Count,
                        fetchedBlocks.Count,
                        batchToSaveToDb.Count,
                        dur.TotalMilliseconds
                        );
                    //_log.LogInformation("Timings: ({0}ms, {1}ms pb, {2}ms slp, {3}ms burn, {4}ms ps, {5}ms vt)",
                    //    dur.TotalMilliseconds,
                    //    stat.ProcessBlock.TotalMilliseconds,
                    //    stat.ProcessBlockSlpTxs.TotalMilliseconds,
                    //    stat.ProcessBlockBurnTxs.TotalMilliseconds,
                    //    stat.TransactionsTime.TotalMilliseconds,
                    //    stat.ValidationTime.TotalMilliseconds
                    //    );
                }
                else
                {
                    if (height % 10 == 0)
                    {
                        _log.LogInformation("Blocks {0}: NON-SLP of total {1} txs, PreFetch {2}",
                            height,
                            data.Item1.Transactions.Count,
                            fetchedBlocks.Count
                            );
                    }
                    //Console.Write("-");
                }
                if (batchToSaveToDb.Count > batchsize)
                {
                    //if parser was quicker than batch saver then await before next batch save
                    if (saveToDbTask != null)
                    {
                        Console.WriteLine();
                        _log.LogInformation("Waiting for db to finish batch write....");
                        await saveToDbTask;
                        _log.LogInformation("Database batch stored....");
                        saveToDbTask = null;
                    }
                    //do not await this here it should work in its own thread
                    var batchBlocksCount = batchBlocks.Count;
                    saveToDbTask = CommitBatchToDb(
                                    batchCounter++,
                                    new List<SlpTransaction>(batchToSaveToDb),
                                    new List<SlpBlock>(batchBlocks));
                    batchToSaveToDb.Clear();
                    batchBlocks.Clear();

                    //if ( _blockMap.Count - initialBlockMapSize > 100 )
                    //{
                    //    var toLocal = _blockMap.ToDictionary(k => k.Key);
                    //    if (protoBufSaveTask != null)
                    //        await protoBufSaveTask;
                    //    protoBufSaveTask = SaveProtoBufAsync("SlpBlockMapFile", toLocal); //do not block use copy of blocks
                    //}
                }
            }
            if (saveToDbTask != null)
                await saveToDbTask;
            await CommitBatchToDb(
                    batchCounter++,
                    new List<SlpTransaction>(batchToSaveToDb),
                    new List<SlpBlock>(batchBlocks));
            //if(_blockMap.Count > initialBlockMapSize )
            //    await SaveProtoBufAsync("SlpBlockMapFile", _blockMap);
        }


        private SlpTransaction CheckNonSlpTransactionForSlpBurn(Transaction tr, int? blockheight)
        {
            //if (tr.GetHash().ToString() == "154c842cde872ee0b6cc77f25748076d87b1f1e2f94c041433755b187838778c")
            //{
            //    int bp = 0;
            //}
            SlpTransaction burnTransaction = null;
            var burnedOutputs = new SortedDictionary<int, SlpTransactionOutput>();
            int inputIx = 0;
            foreach (var input in tr.Inputs)
            {
                var sourceTx = input.PrevOut.Hash.ToString();
                var vout = input.PrevOut.N;

                
                //slpTxInputs.Add(slpTxInput);

                //only check valid transactions for burn
                if (_slpTrCache.TryGetValue(sourceTx, out SlpTransaction prevTx) && prevTx.State == SlpTransaction.TransactionState.SLP_VALID)
                {
                    //if we have non-slp transaction that burned slp transaction output
                    //then we add this transaction as burn transaction so we have nextId output link marking transaction output as spent/burned
                    var burnedOutput = prevTx.SlpTransactionOutputs.ElementAt((int)vout);
                    var slpPrefix = _rpcClient.Network.ChainName.GetSlpPrefix().ToString();
                    if (burnedOutput.Address.Address.StartsWith($"{slpPrefix}:")) //we have non slp transactions that burns slp output
                    {
                        burnedOutputs.Add(inputIx, burnedOutput); //
                        //if this transactions was handled then we already have all outputs and inputs processed
                        if (!_slpTrCache.TryGetValue(tr.GetHash().ToString(), out burnTransaction))
                        {
                            //burn transaction is artificial - we do not process all inputs just the ones that burned slp transaction(s) so we can query db for burn tokens
                            burnTransaction = new SlpTransaction()
                            {
                                SlpToken = null,
                                SlpTokenId = null,
                                SlpTokenType = SlpVersionType.None,
                                Type = SlpTransactionType.BURN,
                                State = SlpTransaction.TransactionState.SLP_UNKNOWN,
                                BlockHeight = blockheight,
                                Hash = tr.GetHash().ToBytes(false),
                                AdditionalTokenQuantity = 0,
                                InvalidReason = null,
                                MintBatonVOut = null,
                                TokenOutputSum = 0,
                                TokenInputSum = 0,
                            };
                            _slpTrCache.AddOrReplace(burnTransaction.Hash.ToHex(), burnTransaction);

                            //add all outputs as bch outputs since this is not-slp transaction
                            for(int i = 0;i < tr.Outputs.Count; i++)
                            {
                                var output = tr.Outputs[i];
                                var bchAddress = string.Empty;
                                var ops = output.ScriptPubKey.ToOps().ToArray();
                                if (ops.Any() && ops.First().Code == OpcodeType.OP_RETURN)
                                    bchAddress = SD.UnparsedAddress;
                                else
                                {
                                    var destAddress = output.ScriptPubKey.GetDestinationAddress(_rpcClient.Network);
                                    if (destAddress == null)
                                        continue;
                                    bchAddress = output.ScriptPubKey.GetDestinationAddress(_rpcClient.Network).ToString();
                                }
                                var address = GetOrCreateAddress(bchAddress.ToString(), burnTransaction.BlockHeight);
                                burnTransaction.SlpTransactionOutputs.Add(
                                    new SlpTransactionOutput
                                    {
                                        BlockchainSatoshis = output.Value.ToDecimal(MoneyUnit.Satoshi),
                                        Amount = 0,
                                        AddressId = address.Id,
                                        Address = address,
                                        VOut = i,
                                        SlpTransaction = burnTransaction,
                                    });
                            }
                        }                       
                    }
                }
                inputIx++;
            }

            if (burnTransaction != null)
            {
                var ix = 0;
                foreach (var input in tr.Inputs)
                {
                    var sourceTx = input.PrevOut.Hash.ToString();
                    var vout = input.PrevOut.N;
                    var addr = input.ScriptSig.PaymentScript.GetDestinationAddress(_rpcClient.Network);
                    var address = addr.ToString();

                    SlpAddress outAddress = null;
                    if (burnedOutputs.TryGetValue(ix, out SlpTransactionOutput output))
                        outAddress = output.Address;
                    else
                    {
                        outAddress = new SlpAddress { Address = address };
                        //if (!_usedAddresses.TryGetValue(address, out var usedAddr))
                        //{
                        //    var nextId = _usedAddresses.Any() ? _usedAddresses.Max(a => a.Value.Id) + 1 : 1;
                        //    var address = GetOrCreateAddress(bchAddress.ToString(), burnTransaction.BlockHeight);
                        //    _usedAddresses.TryAdd(address, usedAddr = new SlpAddress { Id = nextId, Address = address, BlockHeight = burnTransaction.BlockHeight});
                        //}
                        //outAddress = usedAddr; /// GetOrCreateAddress(address);
                    }

                    

                    var slpTxInput = new SlpTransactionInput()
                    {
                        SlpAmount = output?.Amount ?? 0,
                        BlockchainSatoshis = output?.BlockchainSatoshis ?? 0,
                        Address = outAddress,
                        VOut = (int)vout,
                        SourceTxHash = sourceTx.FromHex(),
                        SlpTransaction = burnTransaction
                    };
                    burnTransaction.SlpTransactionInputs.Add(slpTxInput);
                    ix++;
                }
            }
            return burnTransaction;
        }


        class Stat
        {
            public TimeSpan ValidationTime { get; set; } = new TimeSpan();
            public TimeSpan ProcessBlock { get; set; } = new TimeSpan();
            public TimeSpan ProcessBlockSlpTxs { get; set; } = new TimeSpan();
            public TimeSpan ProcessBlockBurnTxs { get; set; } = new TimeSpan();
            public TimeSpan TransactionsTime { get; set; } = new TimeSpan();
        }

        private async Task<Dictionary<string, SlpTransaction>> ProcessBlock(
            Tuple<Block, List<SlpTransaction>, List<Transaction>> data,
            Stat stat
            )
        {
            var start = DateTime.Now;
            var checkForSlpBurnTxs = new ConcurrentBag<Transaction>();
            var burnTxs = new ConcurrentBag<SlpTransaction>();
            var slpTransactions = new ConcurrentBag<SlpTransaction>();

            //process slp transactions
            var startSlpTxs = DateTime.Now;
            
            foreach (var slpTr in data.Item2)
            {
                var tx = slpTr.Hash.ToHex();

                if ( (slpTr.Type == SlpTransactionType.SEND || slpTr.Type == SlpTransactionType.MINT) &&
                    !_tokenMap.ContainsKey(slpTr.SlpTokenId.ToHex())
                    ) //skip here all transactions that does not yet have proper token id
                {
                    _log.LogWarning("Parse slp tx {0} does not have valid token id reference {1}", 
                        slpTr.Hash.ToHex(), slpTr.SlpTokenId.ToHex());
                    continue; //to next
                }
                    

                _slpTrCache.AddOrReplace(tx, slpTr);
                if (slpTr.Type == SlpTransactionType.GENESIS && !_tokenMap.ContainsKey(slpTr.Hash.ToHex()))
                    _tokenMap.TryAdd(slpTr.Hash.ToHex(), slpTr.SlpToken);
                //slpTr.TokenInputSum = slpTr.SlpTransactionInputs.Sum(i => i.SlpAmount);
                slpTr.TokenOutputSum = slpTr.SlpTransactionOutputs.Sum(i => i.Amount);
                slpTransactions.Add(slpTr);
            }
            stat.ProcessBlockSlpTxs = DateTime.Now - startSlpTxs;


            var startBurnTxsCheck = DateTime.Now;
            if (data.Item3 != null && data.Item3.Any())
            {
                Parallel.ForEach(data.Item3, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (tr) =>
                {
                    checkForSlpBurnTxs.Add(tr);
                    var height = data.Item1.GetCoinbaseHeight();
                    if (height != null)
                    {
                        var burnTx = CheckNonSlpTransactionForSlpBurn(tr, height.Value);
                        if (burnTx != null)
                            burnTxs.Add(burnTx);
                    }
                    //try { _notSlpTxCache.Add(tr.GetHash().ToString()); } catch { }
                });
            }
            stat.ProcessBlockBurnTxs = DateTime.Now - startBurnTxsCheck;


            var startTxs = DateTime.Now;
            var blockTxCache = slpTransactions.ToDictionary(k => k.Hash.ToHex());
            var stack = new List<string>();
            TopologicalSort(blockTxCache, stack);
            if (stack.Count != blockTxCache.Count)
                throw new Exception("Transaction count is incorrect after topological sorting.");
            for (int ix = 0; ix < stack.Count; ix++)
            {
                var txId = stack[ix];
                var slpTr = blockTxCache.TryGet(txId);
                if (slpTr == null)
                    throw new Exception("Failed to get transaction!");
                foreach (var i in slpTr.SlpTransactionInputs)
                {
                    if (!_slpTrCache.TryGetValue(i.SourceTxHash.ToHex(), out SlpTransaction sourceTr))
                        continue; //non - slp transaction
                    if (sourceTr.Type == SlpTransactionType.BURN)
                        continue; //no need to process burn transaction since it cannot have slp outputs
                                  //link from input back to output
                    if (sourceTr.SlpTransactionOutputs.Count <= i.VOut)
                        throw new NullReferenceException($"Invalid output reference at index {i.VOut} for tx {sourceTr.Hash.ToHex()}");
                    var output = sourceTr.SlpTransactionOutputs.ElementAt(i.VOut);
                    //i.SlpSourceTransactionOutputId = output.Id;
                    //i.SlpSourceTransactionOutput = output;
                    //link from output to input where output was spent ( if spent )
                    output.NextInput = i;
                    i.Address = output.Address;
                    i.BlockchainSatoshis = output.BlockchainSatoshis;
                    i.SlpAmount = output.Amount;
                }
                var valStart = DateTime.Now;
                // var validState = await _slpValidator.IsValidAsync(txId, null); //TODO: check again slp validate, do we need token filter here, it seem to slow down validation a lot??
                var validState = await _slpValidator.IsValidAsync(txId, null);
                if (validState.Item1)
                    slpTr.State = SlpTransaction.TransactionState.SLP_VALID;
                else
                {
                    slpTr.State = SlpTransaction.TransactionState.SLP_INVALID;
                    slpTr.InvalidReason = validState.Item2;
                    _log.LogWarning("Transaction {0} is invalid due to: {1}", slpTr.Hash.ToHex(), validState.Item2);
                }
                stat.ValidationTime += (DateTime.Now - valStart);
            }
            //add all burn transations to current block slp transaction so they are then stored into database
            foreach (var burnTx in burnTxs)
                blockTxCache.Add(burnTx.Hash.ToHex(), burnTx);

            stat.TransactionsTime = DateTime.Now - startTxs;

            stat.ProcessBlock = DateTime.Now - start;
            return blockTxCache;
        }

        private void TopologicalSort(Dictionary<string, SlpTransaction> transactions, List<string> stack)
        {
            var visited = new HashSet<string>();
            foreach (var tx in transactions)
            {
                if (!visited.Contains(tx.Key))
                {
                    if (stack.Count > 0 && stack.Count % 1000 == 0)
                    {
                        var self = this;
                        self.TopologicalSortInternal(0, tx.Value, transactions, stack, visited);
                    }
                    else
                    {
                        TopologicalSortInternal(0, tx.Value, transactions, stack, visited);
                    }
                }
            }
        }
        private void TopologicalSortInternal(
            int counter,
            SlpTransaction tr,
            Dictionary<string, SlpTransaction> transactions,
            List<string> stack,
            HashSet<string> visited)
        {
            visited.Add(tr.Hash.ToHex());
            foreach (var outpoints in tr.SlpTransactionInputs)
            {
                var prevTx = outpoints.SourceTxHash.ToHex();
                if (visited.Contains(prevTx) || !transactions.ContainsKey(prevTx))
                    continue;
                if (counter > 0 && counter % 1000 == 0)
                {
                    var self = this;
                    self.TopologicalSortInternal(++counter, transactions.TryGet(prevTx), transactions, stack, visited);
                }
                else
                {
                    TopologicalSortInternal(++counter, transactions.TryGet(prevTx), transactions, stack, visited);
                }
            }
            stack.Add(tr.Hash.ToHex());
        }




        private async Task RemoveReorgTransactionsAtHeightAsync(SlpDbContext db, int blockHeight)
        {
            var reorged = await db.SlpTransaction.Where(t => t.BlockHeight.Value >= blockHeight).ToListAsync();
            foreach (var t in reorged)
            {
                _log.LogInformation("Delete txn from graph: {0}", t.Hash.ToHex());
            }
            _log.LogWarning($"Deleting all transactions with block greater than or equal to {blockHeight}.");

            //delete all data from blockheight forward
            await DeleteTransactionsNewerThanBlockHeightAsync(blockHeight);
        }

        public class ReorgCheckResult
        {
            public int Height { get; set; }
            public string HashHex { get; set; }
            public bool  HadReorg { get; set; }
        }

        async Task<ReorgCheckResult> CheckForReorgAsync(Block newBlock)
        {
            using var db = new SlpDbContext(_dbOptions);
            //var hash = newBlock.GetHash().ToString();
            var newBlockHeight = newBlock.GetCoinbaseHeight().Value;
            var newBlockHash = newBlock.GetHash().ToString();
            _log.LogInformation("Checking for reorg for {0} at height...", newBlockHash, newBlockHeight);
            var previousBlockHash = newBlock.Header.HashPrevBlock;
            var lastBlockInDb = await db.SlpBlock.OrderByDescending(b => b.Height).FirstAsync();
            if (lastBlockInDb.Hash.ToHex() == previousBlockHash.ToString() && newBlockHeight == lastBlockInDb.Height + 1)
            {
                _log.LogInformation("Received valid next block. No reorg is needed.");
                return new ReorgCheckResult { HadReorg = false, HashHex = newBlockHash, Height = newBlockHeight };
            }
            //check for reorg by moving back from new block up to the
            _log.LogInformation("Next block does not match our db block tip. Finding common ancestor...");
            SlpBlock blockToResync = null;
            int counter = 0;
            do
            {
                var prevBlock = await _rpcClient.GetBlockAsync(previousBlockHash);
                var prevBlockHeight = prevBlock.GetCoinbaseHeight().Value;
                _log.LogInformation("Checking previous block {0} at {1}", previousBlockHash, prevBlockHeight);
                blockToResync = await db.SlpBlock.FirstOrDefaultAsync(b => b.Hash == previousBlockHash.ToBytes(false));
                counter++;
                if (counter > 100)
                {
                    throw new Exception("Reorg greater that 100 blocks. Do a full resync!");
                }
                if(prevBlock != null)
                    previousBlockHash = prevBlock.Header.HashPrevBlock;
            }
            while (blockToResync == null); //as soon as we find valid previous block in db we stop searching
            _log.LogInformation("Reorg common block found {0} at {1}", blockToResync.Hash.ToHex(), blockToResync.Height);
            _log.LogInformation("Removing all data greater than {0}", blockToResync.Height);
            await DeleteTransactionsNewerThanBlockHeightAsync(blockToResync.Height);

            return new ReorgCheckResult { HadReorg = true, HashHex = blockToResync.Hash.ToHex(), Height = blockToResync.Height };

        }

        async Task<ReorgCheckResult> CheckForBlockReorg(Block newBlock, SlpDbContext db)
        {
            // first, find a height with a block hash - should normallly be found on first try, otherwise rollback
            //(await Info.getNetwork()) === 'mainnet' ? Config.core.from : Config.core.from_testnet;
            var lastCheckPointHeight = newBlock.GetCoinbaseHeight().Value;
            var lastCheckPointHash = newBlock.GetHash().ToString();
            var fromHeight = _configuration.GetValue(nameof(SD.StartFromBlock),SD.StartFromBlock);
            var hadReorg = false;
            string actualHash = null;
            var maxRollback = 100;
            var rollbackCount = 0;
            while (actualHash == null)
            {
                try
                {
                    _log.LogInformation("Checking for reorg for {0}", lastCheckPointHeight);
                    actualHash = (await _rpcClient.GetBlockHashAsync(lastCheckPointHeight)).ToString();
                    _log.LogInformation($"Confirmed actual block hash: ${actualHash} at ${lastCheckPointHeight}");
                }
                catch (Exception)
                {
                    if (lastCheckPointHeight > fromHeight)
                    {
                        _log.LogWarning($"Missing actual hash for height ${lastCheckPointHeight}, rolling back.");
                        lastCheckPointHash = null;
                        await RemoveReorgTransactionsAtHeightAsync(db,lastCheckPointHeight);
                        lastCheckPointHeight--;
                        rollbackCount++;
                        hadReorg = true;
                    }
                    else
                    {
                        _log.LogWarning("Cannot rollback further than {0}.", lastCheckPointHeight);
                    }
                }
                if (rollbackCount > 0 && lastCheckPointHeight > fromHeight)
                {
                    _log.LogWarning("Current checkpoint set to {0} {1} after rollback.", actualHash, lastCheckPointHeight);
                    await db.UpdateSlpStateAsync((int)lastCheckPointHeight, actualHash);
                }
                else if (lastCheckPointHeight <= fromHeight)
                {
                    return new ReorgCheckResult { Height = fromHeight, HashHex = string.Empty, HadReorg = true };
                }
                if (maxRollback > 0 && rollbackCount > maxRollback)
                {
                    throw new Exception("A large rollback occurred when trying to find actual block hash, this should not happen, shutting down");
                }
            }
            if (hadReorg)
                _log.LogInformation("Checkpoint was rolled back because db checkpoint is ahead of the chain tip.");
            else
                _log.LogInformation("Checkpoint is as least as long as the chain height.");

            // Make sure the current tip hash matches chain best hash, otherwise we need to rollback again
            var storedBlock = await db.SlpBlock.FirstOrDefaultAsync(b => b.Height == lastCheckPointHeight);
            _log.LogInformation($"Stored hash: {storedBlock?.Hash.ToHex()} at {lastCheckPointHeight}");
            if (storedBlock != null) //if stored block is null -> normal scenario 
            {
                _log.LogInformation($"Stored hash: {storedBlock?.Hash.ToHex()} at {lastCheckPointHeight}");
                    maxRollback = 100;
                rollbackCount = 0;
                while (storedBlock.Hash.ToHex() != actualHash && lastCheckPointHeight > fromHeight)
                {
                    await RemoveReorgTransactionsAtHeightAsync(db, storedBlock.Height);
                    //await this.removeReorgTransactionsAtHeight(lastCheckpoint.height);
                    storedBlock.Height--;
                    rollbackCount++;
                    hadReorg = true;
                    var actualBlockHash = await _rpcClient.GetBlockHashAsync(storedBlock.Height);
                    actualHash = actualBlockHash.ToString();
                    storedBlock = await db.SlpBlock.FirstOrDefaultAsync(b => b.Height == storedBlock.Height);
                    _log.LogWarning($"Rolling back to stored previous height {storedBlock.Height}");
                    _log.LogWarning($"Rollback - actual hash {actualHash}");
                    _log.LogWarning($"Rollback - stored hash {storedBlock.Hash.ToHex()}");
                    if (maxRollback > 0 && rollbackCount > maxRollback)
                        throw new Exception("A large rollback occurred when rolling back due to prev hash mismatch, this should not happen, shutting down");
                }
                if (rollbackCount > 0 && lastCheckPointHeight > fromHeight)
                {
                    _log.LogWarning($"Current checkpoint at {actualHash} {lastCheckPointHeight}");
                    await db.UpdateSlpStateAsync(storedBlock.Height, actualHash);
                }
                else if (lastCheckPointHeight <= fromHeight)
                {
                    return new ReorgCheckResult { Height = fromHeight, HashHex = (string)null, HadReorg = true };
                }
            }
          
            // return current checkpoint - if a rollback occured the returned value will be for the matching previous block hash
            var result = new ReorgCheckResult { HashHex = actualHash, Height = lastCheckPointHeight, HadReorg=hadReorg };
            _log.LogInformation("Returning result from check for reorg...");
            return result;
        }

        public async Task DeleteSlpTransactionsNewerThanBlockHeightRawPostgreAsync(int blockHeight, int commandsTimeout = SD.TimeConsumingQueryTimeoutSeconds)
        {
            using var db = new SlpDbContext(_dbOptions);
            db.Database.SetCommandTimeout(commandsTimeout);
            var dbConnection = db.Database.GetDbConnection();
            if (!(dbConnection is Npgsql.NpgsqlConnection pgConnection))
                throw new Exception("Only postgre sql is currently imported!");
            _log.LogInformation("Storing to db...");
            if (pgConnection.State != System.Data.ConnectionState.Open)
                pgConnection.Open();

            {
                _log.LogInformation("Reseting all outputs spent in inputs >= than block height {0}", blockHeight);
                using var cmd = dbConnection.CreateCommand();
                cmd.CommandText = 
$@"select o.""Id"" 
from ""SlpTransactionOutput"" o
left join ""SlpTransactionInput"" i on o.""NextInputId"" = i.""Id""
inner join ""SlpTransaction"" t on i.""SlpTransactionId"" = t.""Id""
where not(o.""NextInputId"" is null) and not(t.""BlockHeight"" is null) and t.""BlockHeight"" >= {blockHeight}
                ";
                _log.LogInformation(cmd.CommandText);
                var ids = new List<object>();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        ids.Add(reader.GetInt32(0));
                    }
                }
                _log.LogInformation("Read {0} ids from db.", ids.Count);
                for (var i = 0; i < ids.Count; i += 50)
                {
                    var idSet = ids.Skip(i).Take(50);
                    if (!idSet.Any())
                        break;
                    var idStrSet = idSet.MergeToDelimitedString();
                    _log.LogInformation($"Updating outputs {idStrSet}...");
                    cmd.CommandText = @$"update ""SlpTransactionOutput"" set ""NextInputId""=null where ""Id"" in ({idStrSet})";
                    var res = await cmd.ExecuteNonQueryAsync();                    
                }
                
                //                cmd.CommandText =
                //        $@"update ""SlpTransactionOutput"" 
                //    set ""NextInputId"" = null
                //from ""SlpTransactionOutput"" o
                //left join ""SlpTransactionInput"" i on o.""NextInputId"" = i.""Id""
                //inner join ""SlpTransaction"" t on i.""SlpTransactionId"" = t.""Id""
                //where not(o.""NextInputId"" is null) and not(t.""BlockHeight"" is null) and t.""BlockHeight"" >= {blockHeight}";
                //                var count = await cmd.ExecuteNonQueryAsync();
                _log.LogInformation("Reset {0} outputs spent in inputs >= than block height {1}", ids.Count, blockHeight);
            }

            {
                _log?.LogInformation("Deleting all outputs >= than block height {0}", blockHeight);
                using var cmd = dbConnection.CreateCommand();
                cmd.CommandText =
$@"delete from ""SlpTransactionOutput"" o
where o.""SlpTransactionId"" in 
(select ""Id"" from ""SlpTransaction"" where ""BlockHeight"" > {blockHeight} and ""Id"" = o.""SlpTransactionId"" )";
                var count = await cmd.ExecuteNonQueryAsync();
                _log?.LogInformation("Deleted {0} outputs", count);
            }

            {
                _log?.LogInformation("Deleting all inputs >= than block height {0}", blockHeight);
                using var cmd = dbConnection.CreateCommand();
                cmd.CommandText =
$@"delete from 
""SlpTransactionInput"" i 
where i.""SlpTransactionId"" in 
(select ""Id"" from ""SlpTransaction"" where ""BlockHeight"" > {blockHeight} and ""Id"" = i.""SlpTransactionId"" )";    
                var count = await cmd.ExecuteNonQueryAsync();
                _log?.LogInformation("Deleted {0} inputs", count);
            }
           
            {
                _log?.LogInformation("Deleting all txs >= than block height {0}", blockHeight);
                using var cmd = dbConnection.CreateCommand();
                cmd.CommandText = $@"delete from ""SlpTransaction"" t where t.""BlockHeight"" >= {blockHeight}";
                var count = await cmd.ExecuteNonQueryAsync();
                _log?.LogInformation("Deleted {0} txs", count);
            }

            //leave addresses in database - no need to delete and read then since they are mapped via dictionary 
            //{
            //    _log?.LogInformation("Deleting all addresses >= than block height {0}", blockHeight);
            //    using var cmd = dbConnection.CreateCommand();
            //    cmd.CommandText = $@"delete from ""SlpAddress"" a where a.""BlockHeight"" >= {blockHeight}";
            //    var count = await cmd.ExecuteNonQueryAsync();
            //    _log?.LogInformation("Deleted {0} txs", count);
            //}

            {
                _log?.LogInformation("Deleting all tokens >= than block height {0}", blockHeight);
                using var cmd = dbConnection.CreateCommand();
                cmd.CommandText = $@"delete from ""SlpToken"" a where a.""BlockHeight"" >= {blockHeight}";
                var count = await cmd.ExecuteNonQueryAsync();
                _log?.LogInformation("Deleted {0} txs", count);
            }

            {
                _log?.LogInformation("Deleting all blocks >= than block height {0}", blockHeight);
                using var cmd = dbConnection.CreateCommand();
                cmd.CommandText = $@"delete from ""SlpBlock"" b where b.""Height"" >= {blockHeight}";
                var count = await cmd.ExecuteNonQueryAsync();
                _log?.LogInformation("Deleted {0} blocks", count);
            }
            
            ////token are never deleted - once inserted into database it can stay there since hex is key and 
            ////dangling tokens does not affect anything
            ////log?.LogInformation("Deleting all slp tokens with zero transactions {0}", blockHeight);
            //////TODO: this bulk extensions query does not work because ORDER BY at the end - check for fixes
            //var tokens = SlpToken
            //    //.AsTracking()
            //    .Include(t => t.Transactions).Where(t => !t.Transactions.Any());
            ////await tokens.BatchDeleteAsync();
            //AttachRange(tokens);
            //SlpToken.RemoveRange(tokens);
            //await SaveChangesAsync();
        }

        public Task DeleteTransactionsNewerThanBlockHeightAsync(int blockHeight)
        {
            if (_databaseBackendType == SD.DatabaseBackendType.POSTGRESQL)
                return DeleteSlpTransactionsNewerThanBlockHeightRawPostgreAsync(blockHeight);
            else if( _databaseBackendType == SD.DatabaseBackendType.MSSQL)
                return DeleteSlpTransactionsNewerThanBlockHeightMSSQLAsync(blockHeight);
            throw new NotSupportedException("Database backend is not supported!");
        }

        public async Task DeleteSlpTransactionsNewerThanBlockHeightMSSQLAsync(int blockHeight, ILogger log = null, int commandsTimeout = SD.TimeConsumingQueryTimeoutSeconds)
        {
            _mainDb.Database.SetCommandTimeout(commandsTimeout);


            log?.LogInformation("Deleting all output nextId links that will be deleted from block height >= {0} forward", blockHeight);
            var outputsClearLinks = _mainDb.SlpTransactionOutput
                                        //.AsNoTracking()
                                        .Include(t => t.NextInput)
                                            .ThenInclude(t => t.SlpTransaction)
                                        .Where(t => t.NextInputId != null &&
                                        (!t.NextInput.SlpTransaction.BlockHeight.HasValue || t.NextInput.SlpTransaction.BlockHeight >= blockHeight));
            var updated = await outputsClearLinks.BatchUpdateAsync(a => new SlpTransactionOutput { NextInputId = null });

            log?.LogInformation("Deleting all inputs >= than block height {0}", blockHeight);
            var lastBlockTxInputs = _mainDb.SlpTransactionInput
                //.AsNoTracking()
                .Include(t => t.SlpTransaction)
                .Where(t => !t.SlpTransaction.BlockHeight.HasValue || t.SlpTransaction.BlockHeight >= blockHeight);
            await lastBlockTxInputs.BatchDeleteAsync();

            log?.LogInformation("Deleting all outputs >= than block height {0}", blockHeight);
            var lastBlockTxOutputs = _mainDb.SlpTransactionOutput
                //.AsNoTracking()
                .Include(t => t.SlpTransaction).Where(t => !t.SlpTransaction.BlockHeight.HasValue || t.SlpTransaction.BlockHeight >= blockHeight);
            await lastBlockTxOutputs.BatchDeleteAsync();

            log?.LogInformation("Deleting all transactions >= than block height {0}", blockHeight);
            var lastBlockTxs = _mainDb.SlpTransaction
                //.AsNoTracking()
                .Where(t => !t.BlockHeight.HasValue || t.BlockHeight >= blockHeight);
            await lastBlockTxs.BatchDeleteAsync();

            log?.LogInformation("Deleting all new blocks >= block height {0}", blockHeight);
            var blocks = _mainDb.SlpBlock
                //.AsNoTracking()
                .Where(t => t.Height >= blockHeight);
            await blocks.BatchDeleteAsync();

            //token are never deleted - once inserted into database it can stay there since hex is key and 
            //dangling tokens does not affect anything
            //log?.LogInformation("Deleting all slp tokens with zero transactions {0}", blockHeight);
            ////TODO: this bulk extensions query does not work because ORDER BY at the end - check for fixes
            var tokens = _mainDb.SlpToken
                //.AsTracking()
                .Include(t => t.Transactions).Where(t => !t.Transactions.Any());
            //await tokens.BatchDeleteAsync();
            _mainDb.AttachRange(tokens);
            _mainDb.SlpToken.RemoveRange(tokens);
            await _mainDb.SaveChangesAsync();
        }
        public async Task PreSyncPrepare()
        {
            if (_rpcClient.Network == NBitcoin.Network.RegTest)
                throw new NotSupportedException("RegTest network not supported!");

            _log.LogInformation($"Retrieving last db block height or first slp block height...");
            var lastBlockHeight = await _mainDb.GetLastSavedCheckpointAsync(); //slpdbinitializer sets tip to first block height
            _log.LogInformation("Removing all data >= {0}", lastBlockHeight);

            var timeout = _configuration.GetValue(nameof(SD.TimeConsumingQueryTimeoutSeconds), SD.TimeConsumingQueryTimeoutSeconds);
            await DeleteTransactionsNewerThanBlockHeightAsync(lastBlockHeight);
            
            _log.LogInformation("Retrieving slp id manager state...");
            _mainDb.LoadDatabaseSlpIdManager();

            await LoadSlpTransactionsCacheFromDb();
        }

        public override async Task SyncWithNetworkAsync()
        {
            try
            {
                await _slpNotificationService.RunAsync();
                _log.LogInformation("Initializing heartbeat...");
                var heartBeatTask = Task.Run(async () => {
                    while (true)
                    {
                        await Task.Delay(SD.HeartBeatInMiliseconds);
                        _slpNotificationService.NotifyHeartBeat(_listenHeartBeatCounter);
                        Console.Write(".");
                    }
                });
                _log.LogInformation("Initializing db...");
                _slpDbInitializer.Initialize();                
                _log.LogInformation("Waiting for node to sync with the network...");
                await WaitForFullNodeSync();
                _log.LogInformation("Prepare structures before db sync...");
                await PreSyncPrepare();
                _log.LogInformation("Syncing slp data started...");
                await SyncBlocksAsync();
                _log.LogInformation("Syncing current mempool...");
                await SyncCurrentMempoolAsync();
                _log.LogInformation("Establising ZMQ listener...");
                ListenToZmqAsync();
                _log.LogInformation("Processing ZMQ events...");
                await ProcessQueue();
            }
            catch (Exception e)
            {
                foreach (var d in e.Data)
                    _log.LogError(d.ToString());
                _log.LogError(e.Message);
                _log.LogError(e.StackTrace);
                _log.LogError($"{nameof(SyncWithNetworkAsync)} failure: Stopping application");
                _hostApplicationLifetime.StopApplication();
            }
        }
    }

   
}