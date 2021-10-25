using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Slp.Common.DataAccess;
using Slp.Common.Interfaces;
using Slp.Common.Options;
using Slp.Common.Services;
using System;

namespace Slp.API
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
            // needed to load configuration from appsettings.json
            services.AddOptions();
            //load general configuration from appsettings.json
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            //load ip rules from appsettings.json
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
            //rate limiting
            services.AddMemoryCache();
            services.AddSingleton<IClientPolicyStore, MemoryCacheClientPolicyStore>();
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();

            services.AddControllersWithViews();
            services.AddSwaggerGen(c =>
            {
                c.EnableAnnotations();
                c.SwaggerDoc("v2", new OpenApiInfo { Title = "Slp.API", Version = "v2" });
            });
            var connectionString = Configuration.GetConnectionString("SlpDbConnection");
            services.AddDbContext<SlpDbContext>(
               options =>
               {
#if DEBUG
                    options.EnableSensitiveDataLogging();
#endif
                    options.UseSqlServer(connectionString);
               },
               contextLifetime: ServiceLifetime.Scoped,ServiceLifetime.Scoped
           );
            services.AddScoped<ISlpDataService, SlpDataService>();
            services.AddScoped(c =>
            {
                var opts = Configuration.GetSection(BchNodeOptions.Position).Get<BchNodeOptions>();
                return opts.CreateClient();
            });

            services.AddResponseCaching();
            // Add framework services.
            services.AddMvc();

            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            // services.AddSingleton<IRateLimitConfiguration, ElmahIoRateLimitConfiguration>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseIpRateLimiting();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.Use(async (context, next) =>
            {
                var cacheDurationInSeconds = Configuration.GetValue("ApiCacheDurationInSeconds", 10);
                context.Response.GetTypedHeaders().CacheControl =
                    new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromSeconds(cacheDurationInSeconds)
                    };
                context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
                    new string[] { "Accept-Encoding" };
                await next();
            });

            app.UseResponseCaching();


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v2/swagger.json", "Slp.API v2"));

        }
    }
}
