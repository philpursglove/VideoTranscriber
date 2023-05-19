using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Configuration;
using VideoTranscriberData;
using VideoTranscriberStorage;
using VideoTranscriberVideoClient;

[assembly: FunctionsStartup(typeof(VideoTranscriberFunctions.Startup))]

namespace VideoTranscriberFunctions;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
    }
}