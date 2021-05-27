using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Slp.Indexer.Extensions;

namespace Slp.Indexer
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Any() && args.First() == "version")
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Console.WriteLine( "Slp.Indexer version " + version);
                return 0;
            }

            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            bool verbose = args.Any(a => a == "--verbose");

            LogEventLevel logEventLevel = LogEventLevel.Information;
            if (verbose)
                logEventLevel = LogEventLevel.Debug;
            var switchLevel = new LoggingLevelSwitch(logEventLevel);

            AppDomain appDomain = AppDomain.CurrentDomain;
            appDomain.UnhandledException += AppDomain_UnhandledException;
            
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(switchLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(LogEventLevel.Information)
            .WriteTo.RollingFile("Logs/{Date}.log", LogEventLevel.Debug, retainedFileCountLimit: 365)
            .CreateLogger();

            Log.Logger.Information("Logger initialized...");
            try
            {
                var hostBuilder = CreateHostBuilder(args);
                var app = hostBuilder.Build();
                app.Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                Thread.Sleep(5000);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            //var builder = new ConfigurationBuilder()
            //.AddJsonFile($"appsettings.json", true, true)
            //.AddJsonFile($"appsettings.{environmentName}.json", true, true)
            //.AddEnvironmentVariables();
            var builder = Host.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseSerilog();
            
            return builder;
        }

        private static void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("AppDomain handler caught : " + e.Message);
            Console.WriteLine("Runtime terminating: {0}", args.IsTerminating);
            Thread.Sleep(5000);
        }
    }
}
