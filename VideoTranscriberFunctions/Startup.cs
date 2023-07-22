using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using VideoTranscriberData;
using VideoTranscriberStorage;
using VideoTranscriberVideoClient;

[assembly: FunctionsStartup(typeof(VideoTranscriberFunctions.Startup))]

namespace VideoTranscriberFunctions;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        var configuration = builder.Services.BuildServiceProvider().GetService<IConfiguration>();

        var indexerType = configuration.GetValue<string>("IndexerType").ToLower();
        switch (indexerType)
        {
            case "arm":
                builder.Services.AddScoped(typeof(IVideoIndexerClient), _ => new VideoIndexerClientArm(configuration["SubscriptionId"], 
                    configuration["ResourceGroupName"], configuration["AccountName"]));
                break;
            case "classic":
                builder.Services.AddScoped(typeof(IVideoIndexerClient), _ => new VideoIndexerClientClassic(configuration["ApiKey"], 
                    configuration["AccountId"], configuration["Location"]));
                break;
            default:
                throw new ArgumentException($"IndexerType {indexerType} is not supported");
        }

        builder.Services.AddSingleton(typeof(ITranscriptionDataRepository),
            _ => new TranscriptionDataCosmosRepository(configuration.GetConnectionString("CosmosDbConnectionString")));
        builder.Services.AddScoped(typeof(IStorageClient),
            _ => new AzureStorageClient(configuration.GetConnectionString("StorageAccount"),
                configuration["StorageAccountName"]));
        builder.Services.AddScoped(typeof(ConfigValues), _ => new ConfigValues
        {
            CallbackUrl = new Uri(configuration["CallbackUri"])
        });
    }

    public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    {
        FunctionsHostBuilderContext context = builder.GetContext();

        builder.ConfigurationBuilder
            .SetBasePath(context.ApplicationRootPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{context.EnvironmentName}.json", optional: true, reloadOnChange: false)
            .AddUserSecrets(Assembly.GetExecutingAssembly(), true, true)
            .AddEnvironmentVariables();
    }
}