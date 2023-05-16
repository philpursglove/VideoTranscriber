using Microsoft.AspNetCore.Http.Features;
using VideoTranscriber;
using VideoTranscriber.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddScoped(typeof(ITranscriptionDataRepository), 
    (sp) => new TranscriptionDataTableRepository(builder.Configuration["StorageAccountName"], 
        builder.Configuration["StorageAccountKey"],
        new Uri(builder.Configuration["TableUri"])));
builder.Services.AddScoped(typeof(IStorageClient),
    (sp) => new AzureStorageClient(builder.Configuration.GetConnectionString("VideoTranscriberStorageAccount"),
        builder.Configuration["ContainerName"]));
builder.Services.AddScoped(typeof(VideoIndexerClient),
    (sp) => new VideoIndexerClient(builder.Configuration["ApiKey"], builder.Configuration["AccountId"],
        builder.Configuration["Location"]));

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.Limits.MaxRequestBodySize = 268435456;
});

builder.Services.Configure<FormOptions>(options =>
{
    // Set the limit to 256 MB
    options.MultipartBodyLengthLimit = 268435456;
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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
