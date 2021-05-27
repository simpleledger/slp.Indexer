using Slp.Common.Models;
using Slp.Common.Models.DbModels;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Slp.Common.Utility;
using Slp.Common.Extensions;

namespace Slp.Common.DataAccess
{
    public partial class SlpDbContext
    {       
        class TableIndex
        {
            public string table; public string index;
        }

        public static async Task DisableHeavyIndicesAsync(DbContextOptions<SlpDbContext> dbOptions, ILogger log = null)
        {
            using var db = new SlpDbContext(dbOptions);
            var indexes = new List<TableIndex>();
            if (db.Database.GetDbConnection().State != ConnectionState.Open)
                await db.Database.OpenConnectionAsync();
            using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText =
            @"SELECT 
                    sys.tables.name as TableName,
                    sys.indexes.name AS IndexName
                FROM sys.indexes
                INNER JOIN sys.tables ON sys.tables.object_id = sys.indexes.object_id
                WHERE
                    sys.indexes.type_desc = 'NONCLUSTERED'
                    AND sys.tables.name != 'BlockchainFile'";

                using var result = await command.ExecuteReaderAsync();
                while (await result.ReadAsync())
                    indexes.Add(new TableIndex() { table = result.GetString(0), index = result.GetString(1) });
            }
            foreach (var index in indexes)
            {
                log?.LogInformation("Disabling index {0}", index.index);
                var sql = $"alter index [{index.index}] on [{index.table}] DISABLE";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        public static async Task RebuildHeavyIndicesAsync(DbContextOptions<SlpDbContext> dbOptions, ILogger log = null)
        {
            using var db = new SlpDbContext(dbOptions);
            var indexes = new List<TableIndex>();
            if (db.Database.GetDbConnection().State != ConnectionState.Open)
                await db.Database.OpenConnectionAsync();
            using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText =
            @"SELECT 
                    sys.tables.name as TableName,
                    sys.indexes.name AS IndexName
                FROM sys.indexes
                INNER JOIN sys.tables ON sys.tables.object_id = sys.indexes.object_id
                WHERE
                    sys.indexes.type_desc = 'NONCLUSTERED'
                    AND sys.tables.name != 'BlockchainFile'";

                using var result = await command.ExecuteReaderAsync();
                while (await result.ReadAsync())
                    indexes.Add(new TableIndex() { table = result.GetString(0), index = result.GetString(1) });
            }
            db.Database.SetCommandTimeout(600);
            foreach (var index in indexes)
            {
                log?.LogInformation("Rebuilding index {0}...", index.index);
                var sql = $"alter index [{index.index}] on [{index.table}] REBUILD";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }
        public async Task DeleteSlpTransactionsNewerThanBlockHeight(int blockHeight,ILogger log = null, int commandsTimeout = SD.TimeConsumingQueryTimeoutSeconds)
        {
            Database.SetCommandTimeout(commandsTimeout);

            log?.LogInformation("Deleting all output nextId links that will be deleted from block height >= {0} forward", blockHeight);
            var outputsClearLinks = SlpTransactionOutput
                                        //.AsNoTracking()
                                        .Include(t => t.NextInput)
                                            .ThenInclude(t => t.SlpTransaction)
                                        .Where(t => t.NextInputId != null && 
                                        (!t.NextInput.SlpTransaction.BlockHeight.HasValue || t.NextInput.SlpTransaction.BlockHeight >= blockHeight))
                                        ;
            var updated = await outputsClearLinks.BatchUpdateAsync(a => new SlpTransactionOutput { NextInputId = null });

            log?.LogInformation("Deleting all inputs >= than block height {0}", blockHeight);
            var lastBlockTxInputs = SlpTransactionInput
                //.AsNoTracking()
                .Include(t => t.SlpTransaction)
                .Where(t => !t.SlpTransaction.BlockHeight.HasValue || t.SlpTransaction.BlockHeight >= blockHeight);
            await lastBlockTxInputs.BatchDeleteAsync();
            
            log?.LogInformation("Deleting all outputs >= than block height {0}", blockHeight);
            Database.SetCommandTimeout(120);
            var lastBlockTxOutputs = SlpTransactionOutput
                //.AsNoTracking()
                .Include(t => t.SlpTransaction).Where(t => !t.SlpTransaction.BlockHeight.HasValue || t.SlpTransaction.BlockHeight >= blockHeight);
            await lastBlockTxOutputs.BatchDeleteAsync();

            log?.LogInformation("Deleting all transactions >= than block height {0}", blockHeight);
            var lastBlockTxs = SlpTransaction
                //.AsNoTracking()
                .Where(t => !t.BlockHeight.HasValue || t.BlockHeight >= blockHeight);
            await lastBlockTxs.BatchDeleteAsync();

            log?.LogInformation("Deleting all new blocks >= block height {0}", blockHeight);
            var blocks = SlpBlock
                //.AsNoTracking()
                .Where(t => t.Height >= blockHeight);
            await blocks.BatchDeleteAsync();

            //token are never deleted - once inserted into database it can stay there since hex is key and 
            //dangling tokens does not affect anything
            //log?.LogInformation("Deleting all slp tokens with zero transactions {0}", blockHeight);
            ////TODO: this bulk extensions query does not work because ORDER BY at the end - check for fixes
            var tokens = SlpToken
                //.AsTracking()
                .Include(t => t.Transactions).Where(t => !t.Transactions.Any());
            //await tokens.BatchDeleteAsync();
            AttachRange(tokens);
            SlpToken.RemoveRange(tokens);
            await SaveChangesAsync();                
        }

        //public async Task RemoveAllDataNewerThanBlockHeightAsync(int blockHeight, ILogger log = null)
        //{
        //    //SLP tables are not linked with internal pointers to blockchain tables so we must change queries here
        //    const string deleteFromSlpTransactionOutput = @"
        //        DELETE [SlpTransactionOutput] FROM [SlpTransactionOutput]
        //        INNER JOIN [SlpTransaction] ON [SlpTransaction].Id = [SlpTransactionOutput].SlpTransactionId
        //        WHERE [SlpTransaction].BlockHeight >= {0}";
        //    const string deleteFromSlpTransactionInput = @"
        //        DELETE [SlpTransactionInput] FROM [SlpTransactionInput]
        //        INNER JOIN [SlpTransaction] ON [SlpTransaction].Id = [SlpTransactionInput].SlpTransactionId
        //        WHERE [SlpTransaction].BlockHeight >= {0}";
        //    const string deleteFromSlpTransaction = @"
        //        DELETE [SlpTransaction] FROM [SlpTransaction]
        //        WHERE [SlpTransaction].BlockHeight >= {0}";
        //    //delete also dangling tokens - tokens that were swept away by delete
        //    const string deleteDanglingSlpToken = @"
        //        DELETE [SlpToken] FROM [SlpToken] as tok
        //        LEFT OUTER JOIN [SlpTransaction] tr ON tok.Id = tr.SlpTokenId
        //        WHERE tr.SlpTokenId = null";

        //    //delete slp
        //    log?.LogInformation("Deleting Slp transaction inputs...");
        //    await Database.ExecuteSqlRawAsync(deleteFromSlpTransactionInput, blockHeight);
        //    log?.LogInformation("Deleting Slp transaction outputs...");
        //    await Database.ExecuteSqlRawAsync(deleteFromSlpTransactionOutput, blockHeight);
        //    log?.LogInformation("Deleting Slp transactions...");
        //    await Database.ExecuteSqlRawAsync(deleteFromSlpTransaction, blockHeight);
        //    log?.LogInformation("Deleting Slp tokens...");
        //    await Database.ExecuteSqlRawAsync(deleteDanglingSlpToken);
        //}

        public void AssignTransactionsNewId(IEnumerable<SlpTransaction> slpTransactions, int? blockHeight)
        {
            if (DbSlpIdManager == null)
                throw new NullReferenceException("To assign new ids DbSlpIdManager must be loaded on this contenx!");
            if (slpTransactions == null)
                return;
            foreach (var tr in slpTransactions)
            {
                if(blockHeight.HasValue)
                    tr.BlockHeight = blockHeight;
                tr.Id = DbSlpIdManager.GetNextSlpTransactionId();
                foreach (var o in tr.SlpTransactionOutputs)
                {
                    o.Id = DbSlpIdManager.GetNextSlpTransactionOutputId();
                    o.SlpTransactionId = tr.Id;
                    o.SlpTransaction = tr;
                }
                foreach (var i in tr.SlpTransactionInputs)
                {
                    i.Id = DbSlpIdManager.GetNextSlpTransactionInputId();
                    i.SlpTransactionId = tr.Id;
                    i.SlpTransaction = tr;
                }            
            }
        }

        public Task<SlpBlock> GetBlockTipAsync()
        {
            return SlpBlock.OrderByDescending(b => b.Height).FirstAsync();
        }

        public async Task<int> GetLastSavedCheckpointAsync()
        {           
            var dbState = await SlpDatabaseState.FindAsync(1);
            return dbState.BlockTip;
            //if (dbState.BlockTip == null) //from scratch
            //    lastBlockHeight = slpFromHeight; //to start from SlpBlockHeightFrom
            //else
            //{
            //    lastBlockHeight = dbState.LastCheckPointBlockHeight.Value;
            //    lastBlockHeight++; //continue with next block

            //    await DeleteSlpTransactionsNewerThanBlockHeight((uint)lastBlockHeight, log);
            //}
            //return lastBlockHeight;
        }

        public void LoadDatabaseSlpIdManager()
        {
            var slpTransactionId = 0L;
            if (SlpTransaction.Any())
                slpTransactionId = SlpTransaction.Select(b => b.Id).Max();
            var slpTransactionInputId = 0L;
            if (SlpTransactionInput.Any())
                slpTransactionInputId = SlpTransactionInput.Select(b => b.Id).Max();
            var slpTransactionOutputId = 0L;
            if ( SlpTransactionOutput.Any())
                slpTransactionOutputId = SlpTransactionOutput.Select(b => b.Id).Max();
            DbSlpIdManager = new DbSlpIdManager(slpTransactionId, slpTransactionInputId, slpTransactionOutputId);
        }

        //public async Task RemoveTransaction(string txHex, bool? unconfirmed = null)
        //{
        //    var txs = await SlpTransaction.Where(t => t.Hex == txHex && t.Unconfirmed == unconfirmed).ToArrayAsync();
        //    var txis = await SlpTransactionInput
        //        .Include(t => t.SlpTransaction)
        //        .Where(i => i.SlpTransaction.Hex == txHex && i.SlpTransaction.Unconfirmed == unconfirmed)
        //        .ToArrayAsync();
        //    var txos = await SlpTransactionOutput
        //        .Include(t => t.SlpTransaction)
        //        .Where(i => i.SlpTransaction.Hex == txHex && i.SlpTransaction.Unconfirmed == unconfirmed)
        //        .ToArrayAsync();

        //    SlpTransactionOutput.RemoveRange(txos);
        //    SlpTransactionInput.RemoveRange(txis);
        //    SlpTransaction.RemoveRange(txs);

        //    await SaveChangesAsync();
        //}

        //public async Task RemoveAllUnconfirmedSlpTransactions()
        //{
        //    var txs = SlpTransaction.Where(t => t.Unconfirmed == true);
        //    var txis = SlpTransactionInput.Include(t=>t.SlpTransaction).Where(t => t.SlpTransaction.Unconfirmed == true);
        //    var txos = SlpTransactionOutput.Include(t => t.SlpTransaction).Where(t => t.SlpTransaction.Unconfirmed == true);

        //    await txis.BatchDeleteAsync();
        //    await txos.BatchDeleteAsync();
        //    await txs.BatchDeleteAsync();

        //    //SlpTransactionInput.RemoveRange(txis);
        //    //SlpTransactionOutput.RemoveRange(txos);            
        //    //SlpTransaction.RemoveRange(txs);

        //    //await SaveChangesAsync();

            
        //}

        //public async Task UpdateSlpCheckpointToNewHeightAsync(int height, string hash)
        //{
        //    var state = await SlpDatabaseState.FirstAsync();
        //    state.BlockTip = height;
        //    state.BlockTipHash = hash;
        //    if (hash != null)
        //    {
        //        var block = await SlpBlock.FindAsync(hash);
        //        if (block != null)
        //            block.Height = height;
        //        else
        //        {
        //            await SlpBlock.AddAsync(new SlpBlock { Hex = hash, Height = height, IsSlp = 1 });
        //        }
        //    }
        //    await SaveChangesAsync();
        //}

        public async Task UpdateSlpStateAsync(int blockHeightTip, string blockHashTip)
        {
            var state = await SlpDatabaseState.FindAsync(1);
            state.BlockTip = blockHeightTip;
            state.BlockTipHash = blockHashTip;
            state.LastStatusUpdate = DateTime.Now;
            await SaveChangesAsync();
        }


        public async Task<SlpTransactionOutput[]> GetAllUnspentTokenOutputsAsync(string tokenHex)
        {
            var tokenSlpBalances = await SlpTransactionOutput
                                .Include(o => o.SlpTransaction)
                                .Where(o => !o.NextInputId.HasValue && o.Amount >= 0 && o.SlpTransaction.Hash == tokenHex.FromHex())
                                .ToArrayAsync();
            return tokenSlpBalances;
        }

        public async Task<SlpTransactionOutput[]> GetAllUnspentTokenOutputsForAddressAsync(string tokenHex,string slpAddress)
        {
            var tokenSlpBalances = await SlpTransactionOutput
                                .Include(o => o.SlpTransaction)
                                .Include(o => o.Address)
                                .Where(o => !o.NextInputId.HasValue && o.Amount >= 0 && 
                                o.SlpTransaction.SlpTokenId == tokenHex.FromHex() &&
                                o.Address.Address == slpAddress)
                                .ToArrayAsync();
            return tokenSlpBalances;
        }

        //public async Task<AddressUtxos> GetAddressUtxosAsync(string address)
        //{
        //    address = address.ToPrefixedSlpAddress();
        //    var utxoOutputs = SlpTransactionOutput
        //        .Include(t => t.SlpTransaction)
        //            .ThenInclude(t => t.SlpTransactionOutputs)
        //        .Where(o => o.Address == address && !o.NextInputId.HasValue).ToArray();
        //    var res = new AddressUtxos();
        //    res.CashAddress = address.ToAddress(AddressPrefix.bitcoincash);
        //    res.LegacyAddress = "";
        //    res.SlpAddress = address;
        //    res.
        //    res.Utxos = new Utxo[utxoOutputs.Length];
        //    int counter = 0;
        //    foreach (var utxoOut in utxoOutputs)
        //    {
        //        var vout = utxoOut.SlpTransaction.SlpTransactionOutputs.IndexOf(utxoOut);
        //        res.Utxos[counter] = new Utxo()
        //        {
        //            Satoshis = utxoOut.BlockchainSatoshis,
        //            TxId = utxoOut.SlpTransactionHex,
        //            VOut = vout,
        //            SlpUtxoJudgement = Enums.SlpUtxoJudgement.SLP_TOKEN,
        //            SlpUtxoJudgementAmount = utxoOut.SlpAmmount,                    
        //        };
        //    }
        //    //var url = RestV2 + string.Format(address_utxo, address);
        //    //var result = await _httpClient.GetAsync(url);
        //    //if (!result.IsSuccessStatusCode)
        //    //    throw new Exception("Failed to retrieve utxo for address ");
        //    //{
        //    //    var content = await result.Content.ReadAsStringAsync();
        //    //    return JsonConvert.DeserializeObject<AddressUtxos>(content);
        //    //}

        //}
    }
}
