using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Threading.Tasks;

namespace Slp.Indexer.Services
{
    public abstract class IndexerServiceBase :  IIndexerService
    {
        protected readonly RPCClient _rpcClient;
        protected readonly ILogger<IndexerServiceBase> _log;
        protected readonly IConfiguration _configuration;
        protected readonly IHostApplicationLifetime _hostApplicationLifetime;
        protected readonly IServiceProvider _serviceProvider;
        
        protected int _listenHeartBeatCounter = -1;

        public IndexerServiceBase(
            RPCClient rpcClient,
            IConfiguration configuration,
            IHostApplicationLifetime hostApplicationLifetime,
            IServiceProvider serviceProvider,
            ILogger<IndexerServiceBase> log)
        {
            _rpcClient = rpcClient;
            _configuration = configuration;
            _log = log;
            _serviceProvider = serviceProvider;
            _hostApplicationLifetime = hostApplicationLifetime;
        }
        public abstract Task SyncWithNetworkAsync();
        protected async Task WaitForFullNodeSync()
        {
            while (true)
            {
                try
                {
                    var info = await _rpcClient.GetBlockchainInfoAsync();
                    var chain = info.Chain;
                    //if (chain.ChainName == NetworkType.Regtest)
                    if (chain.ChainName == ChainName.Regtest)
                        break;
                    var syncdBlocks = info.Blocks;
                    // isSyncd = syncdBlocks == networkBlocks ? true : false;
                    if (info.VerificationProgress < 0.9990f)
                    {
                        _log.LogInformation(" Waiting for bitcoind to sync with network ( blocks count: {0}, progress: {1} )", syncdBlocks, info.VerificationProgress);
                    }
                    else
                        break;
                    await Task.Delay(5000);
                }
                catch (Exception e)
                {
                    _log.LogError(e.Message);
                    _log.LogInformation("Bitcoind is currently not responding. If it is booting up this will go away in a few moments...");
                    await Task.Delay(5000);
                }
            }
        }
    }
}
