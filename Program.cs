using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http.Features;
using SmartGallery.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 25 * 1024 * 1024;
});

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("Storage"));

builder.Services.AddSingleton<BlobImageService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

await using (var scope = app.Services.CreateAsyncScope())
{
    var svc = scope.ServiceProvider.GetRequiredService<BlobImageService>();
    await svc.EnsureContainerAsync();
}

app.Run();