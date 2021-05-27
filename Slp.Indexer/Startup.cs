using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Slp.Indexer.Services;

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
            SlpIndexerService.ConfigureServices(Configuration, services);
            services.AddHostedService<HostedService>();
        }      
    }
}
