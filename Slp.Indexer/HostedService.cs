using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using Slp.Indexer.Services;

namespace Slp.Indexer
{
    public class HostedService : BackgroundService
    {
        private readonly ILogger<HostedService> _log;
        public readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        public HostedService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<HostedService> log,
            IHostApplicationLifetime hostApplicationLifetime
            )
        {
            _serviceScopeFactory = serviceScopeFactory;
            _log = log;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        #region BackgroundService
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _log.LogInformation("Starting service...");
            return base.StartAsync(cancellationToken);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    _log.LogInformation("Executing service...");
                    var syncService = scope.ServiceProvider.GetRequiredService<IIndexerService>();
                    _log.LogInformation("Syncing with live node...");
                    await syncService.SyncWithNetworkAsync();
                    _log.LogError("Sync routing stopped unexpectedly...");
                }
            }
            catch (ApplicationException e)
            {
                _log.LogError("Application exception before shutdown: {0}", e.Message);
                _hostApplicationLifetime.StopApplication();
            }
            
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _log.LogInformation("Stopping service...");
            return base.StopAsync(cancellationToken);
        }
        #endregion
   }
}