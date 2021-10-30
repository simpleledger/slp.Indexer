using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Slp.Common.DataAccess;
using Slp.Common.Interfaces;
using Slp.Common.Options;
using Slp.Common.Services;
using Slp.Common.Utility;
using Slp.Indexer.Services;
using System;
using System.Net.Http;

namespace Slp.Indexer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var databaseType = Configuration.GetValue(nameof(SD.DatabaseBackend), SD.DatabaseBackend);
            var slpConnectionString = Configuration.GetConnectionString("SlpDbConnection");
            if (slpConnectionString == null)
                throw new Exception("Missing SlpDbConnection string.");
            services.AddDbContext<SlpDbContext>(
                options =>
                {
#if DEBUG
                    options.EnableSensitiveDataLogging();
#endif
                    if (databaseType == SD.DatabaseBackendType.POSTGRESQL)
                    {
                        options.UseNpgsql(slpConnectionString, x => x.MigrationsAssembly("Slp.Migrations.POSTGRESQL"));
                    }
                    else if (databaseType == SD.DatabaseBackendType.MSSQL)
                    {
                        options.UseSqlServer(slpConnectionString, x => x.MigrationsAssembly("Slp.Migrations.MSSQL"));
                    }
                    else
                        throw new NotSupportedException();
                },
                contextLifetime: ServiceLifetime.Transient
            );
            // Add app
            services.AddScoped<ISlpDbInitializer, SlpDbInitializer>();
            services.AddScoped<ISlpService, SlpService>();
            services.AddScoped<ISlpValidator, SlpLocalValidationService>();
            services.AddScoped<ISlpNotificationService, SlpNotificationService>();
            services.AddScoped<HttpClient>();

            services.AddScoped<IIndexerService, SlpIndexerService>();
            services.AddTransient((sp) =>
            {
                var nodeOpts = Configuration.GetSection(BchNodeOptions.Position).Get<BchNodeOptions>();
                var client = nodeOpts.CreateClient();
                return client;
            }
           );
            services.AddHostedService<HostedService>();
        }      
    }
}
