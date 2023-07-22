using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Http.Features;
using VideoTranscriber;
using VideoTranscriber.Controllers;
using VideoTranscriberData;
using VideoTranscriberStorage;
using VideoTranscriberVideoClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton(typeof(ITranscriptionDataRepository),
    _ => new TranscriptionDataCosmosRepository(builder.Configuration.GetConnectionString("VideoTranscriberCosmosDb")));
builder.Services.AddScoped(typeof(IStorageClient),
    _ => new AzureStorageClient(builder.Configuration.GetConnectionString("VideoTranscriberStorageAccount"),
        builder.Configuration["ContainerName"]));
builder.Services.AddScoped(typeof(VideoIndexerClientClassic),
    _ => new VideoIndexerClientClassic(builder.Configuration["ApiKey"], builder.Configuration["AccountId"],
        builder.Configuration["Location"]));

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    // By default, all incoming requests will be authorized according to the default policy.
    options.FallbackPolicy = options.DefaultPolicy;
});


builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.Limits.MaxRequestBodySize = 1073741824;
});

builder.Services.Configure<FormOptions>(options =>
{
    // Set the limit to 256 MB
    options.MultipartBodyLengthLimit = 1073741824;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
