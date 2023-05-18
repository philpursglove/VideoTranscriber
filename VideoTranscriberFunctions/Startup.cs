using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        // Azure storage client
        builder.Services.AddScoped(typeof(IStorageClient), (sp) => new AzureStorageClient(configuration[""]));
        // Video Indexer client
        builder.Services.AddScoped(typeof(VideoIndexerClient), (sp) => new VideoIndexerClient(configuration[""]));
        // Repository
        builder.Services.AddScoped(typeof(ITranscriptionDataRepository),
            (sp) => new TranscriptionDataCosmosRepository(configuration[""]));
    }
}