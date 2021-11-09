﻿using Application.Common.Interfaces;
using Application.Options;
using Infrastructure;
using Infrastructure.AzureSynapse;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using SendGrid;
using System;

namespace CredentialServiceJob
{
    public class Startup
    {
        public IServiceCollection ConfigureService()
        {
            var services = ConfigureServices();

            IConfiguration Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<Program>()
                .AddEnvironmentVariables()
                .Build();

            var builder = new ConfigurationBuilder();
            // tell the builder to look for the appsettings.json file
            builder
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            builder.AddUserSecrets<Program>();

            Configuration = builder.Build();

            services
                 .AddTransient<IStartupFilter, OptionsValidationStartupFilter>()

                 .Configure<SendGridSettings>(o => Configuration.GetSection("SendGridSettings").Bind(o))
                 .Configure<AzureSynapseSettings>(o => Configuration.GetSection("AzureSynapseSettings").Bind(o))
                 .Configure<AppSettings>(o => Configuration.GetSection("AppSettings").Bind(o))
                 .Configure<TwilioSettings>(o => Configuration.GetSection("TwilioSettings").Bind(o))
                 .Configure<MessageQueueSettings>(o => Configuration.GetSection("MessageQueueSettings").Bind(o))
                 .Configure<KeySettings>(o => Configuration.GetSection("KeySettings").Bind(o))
                 .AddOptions()
                 .AddLogging(configure => configure.AddApplicationInsightsWebJobs(c => c.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY"))
                .AddConsole(configure =>
                 {
                     // print out only 1 line per message( without this 2 are printed)
                     configure.FormatterName = ConsoleFormatterNames.Systemd;
                 }))

                 .AddSingleton(r => r.GetRequiredService<IOptions<SendGridSettings>>().Value)
                 .AddSingleton(r => r.GetRequiredService<IOptions<AzureSynapseSettings>>().Value)
                 .AddSingleton(r => r.GetRequiredService<IOptions<AppSettings>>().Value)
                 .AddSingleton(r => r.GetRequiredService<IOptions<TwilioSettings>>().Value)
                 .AddSingleton(r => r.GetRequiredService<IOptions<MessageQueueSettings>>().Value)
                 .AddSingleton(r => r.GetRequiredService<IOptions<KeySettings>>().Value)

                 .AddSingleton<ISettingsValidate>(r => r.GetRequiredService<IOptions<SendGridSettings>>().Value)
                 .AddSingleton<ISettingsValidate>(r => r.GetRequiredService<IOptions<AzureSynapseSettings>>().Value)
                 .AddSingleton<ISettingsValidate>(r => r.GetRequiredService<IOptions<AppSettings>>().Value)
                 .AddSingleton<ISettingsValidate>(r => r.GetRequiredService<IOptions<TwilioSettings>>().Value)
                 .AddSingleton<ISettingsValidate>(r => r.GetRequiredService<IOptions<MessageQueueSettings>>().Value)
                 .AddSingleton<ISettingsValidate>(r => r.GetRequiredService<IOptions<KeySettings>>().Value)

                 .AddSingleton<IEmailService, EmailService>()
                 .AddSingleton<IBase64UrlUtility, Base64UrlUtility>()
                 .AddSingleton<IAesEncryptionService, AesEncryptionService>()
                 .AddSingleton<IAesEncryptionService, AesEncryptionService>()
                 .AddSingleton<IAzureSynapseService, AzureSynapseService>()
                 .AddSingleton<IMessagingService, MessagingService>()
                 .AddSingleton<IQueueProcessor, Program>()
                 .BuildServiceProvider();

            AddSendGridClient(services);

            return services;
        }

        private static void AddSendGridClient(IServiceCollection services)
        {
            var options = services.BuildServiceProvider().GetService<IOptions<SendGridSettings>>().Value;

            //Initialize and add context
            var client = new SendGridClient(options.Key);

            services.AddTransient(x => client);
        }

        private static IServiceCollection ConfigureServices()
        {
            IServiceCollection services = new ServiceCollection();

            var config = LoadConfiguration();
            services.AddSingleton(config);

            // required to run the application
            services.AddTransient<EmailService>();
            services.AddTransient<Base64UrlUtility>();
            services.AddTransient<AzureSynapseService>();
            services.AddTransient<AesEncryptionService>();

            return services;
        }

        private static IConfiguration LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<Program>();

            return builder.Build();
        }
    }
}