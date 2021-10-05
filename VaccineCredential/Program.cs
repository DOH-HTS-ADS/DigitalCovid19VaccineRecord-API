using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace VaccineCredential.Api
{
    public class Program
    {
        #region Public Statics

        public static IConfiguration Configuration { get; } = GetConfiguration();

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseConfiguration(Configuration)
                .UseStartup<Startup>()
                .Build();

        #endregion Public Statics

        #region Main Entry

        public static int Main(string[] args)
        {
            var host = BuildWebHost(args);
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation($"Starting {typeof(Program).Namespace} Web Host");
            host.Run();
            return 0;
        }

        #endregion Main Entry

        #region Private Statics

        private static IConfiguration GetConfiguration()
        {
            //Get base config
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            //If were in development, get Secrets
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT").Equals("Development"))
            {
                configuration.AddUserSecrets<Program>();
            }

            return configuration.Build();
        }

        #endregion Private Statics
    }
}