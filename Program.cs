using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http.Features;
using SmartGallery.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 25 * 1024 * 1024;
});

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("Storage"));

var storageOptions = new StorageOptions();
builder.Configuration.GetSection("Storage").Bind(storageOptions);
var configErrors = ConfigurationValidator.ValidateStorageConfiguration(storageOptions);

if (configErrors.Any())
{
    var tempBuilder = WebApplication.CreateBuilder(args);
    var tempApp = tempBuilder.Build();

    tempApp.Use(async (HttpContext context, RequestDelegate next) =>
    {
        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Konfigurationsfejl</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            margin: 0;
            padding: 20px;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }}
        .container {{
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            max-width: 800px;
            width: 100%;
            padding: 40px;
        }}
        h1 {{
            color: #dc3545;
            margin-top: 0;
            font-size: 2em;
            display: flex;
            align-items: center;
            gap: 12px;
        }}
        .icon {{
            font-size: 1.2em;
        }}
        .error-list {{
            background: #fff3cd;
            border: 1px solid #ffc107;
            border-radius: 8px;
            padding: 20px;
            margin: 20px 0;
        }}
        .error-list h2 {{
            margin-top: 0;
            color: #856404;
            font-size: 1.2em;
        }}
        .error-list ul {{
            margin: 10px 0;
            padding-left: 20px;
        }}
        .error-list li {{
            color: #856404;
            margin: 8px 0;
            line-height: 1.6;
        }}
        .help-section {{
            background: #e7f3ff;
            border: 1px solid #2196F3;
            border-radius: 8px;
            padding: 20px;
            margin-top: 20px;
        }}
        .help-section h2 {{
            margin-top: 0;
            color: #0c5460;
            font-size: 1.1em;
        }}
        .help-section p {{
            margin: 10px 0;
            color: #004085;
            line-height: 1.6;
        }}
        .config-example {{
            background: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 6px;
            padding: 15px;
            margin: 15px 0;
            font-family: 'Courier New', monospace;
            font-size: 0.9em;
            overflow-x: auto;
        }}
        .config-example pre {{
            margin: 0;
            white-space: pre-wrap;
        }}
        code {{
            background: #f8f9fa;
            padding: 2px 6px;
            border-radius: 3px;
            font-family: 'Courier New', monospace;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>
            <span class=""icon"">⚠️</span>
            Konfigurationsfejl
        </h1>

        <p>SmartGallery kan ikke starte på grund af manglende eller forkerte konfigurationsindstillinger.</p>

        <div class=""error-list"">
            <h2>Følgende fejl blev fundet:</h2>
            <ul>
                {string.Join("", configErrors.Select(e => $"<li>{e}</li>"))}
            </ul>
        </div>

        <div class=""help-section"">
            <h2>Sådan rettes fejlen:</h2>
            <p>Åbn din <code>appsettings.json</code> eller <code>appsettings.Development.json</code> fil og kontrollér storage-konfigurationen.</p>

            <p><strong>For udvikling (connection string):</strong></p>
            <div class=""config-example"">
                <pre>{{
  ""Storage"": {{
    ""UseManagedIdentity"": false,
    ""ConnectionString"": ""DefaultEndpointsProtocol=https;AccountName=..."",
    ""ContainerName"": ""gallery""
  }}
}}</pre>
            </div>

            <p><strong>For produktion (managed identity):</strong></p>
            <div class=""config-example"">
                <pre>{{
  ""Storage"": {{
    ""UseManagedIdentity"": true,
    ""AccountName"": ""ditStorageAccountNavn"",
    ""ContainerName"": ""gallery""
  }}
}}</pre>
            </div>

            <p>Genstart applikationen efter at have rettet konfigurationen.</p>
        </div>
    </div>
</body>
</html>";
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(html);
    });

    await tempApp.RunAsync();
    return;
}

builder.Services.AddSingleton<BlobImageService>();

var app = builder.Build();

// Try to initialize the service before setting up the pipeline
try
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var svc = scope.ServiceProvider.GetRequiredService<BlobImageService>();
        await svc.EnsureContainerAsync();
    }
}
catch (Exception ex)
{
    var runtimeErrors = new List<string>();

    // If this is a DI resolution error, check the inner exception for the real error
    var actualException = ex;
    if (ex.Message.Contains("Unable to resolve service", StringComparison.OrdinalIgnoreCase) && ex.InnerException != null)
    {
        actualException = ex.InnerException;
    }

    if (actualException is InvalidOperationException)
    {
        runtimeErrors.Add(actualException.Message);
    }
    else if (actualException.InnerException?.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) == true ||
             actualException.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
    {
        if (storageOptions.UseManagedIdentity)
        {
            runtimeErrors.Add("Managed Identity authentication fejlede. Kontrollér at applikationen har adgang til storage kontoen.");
            runtimeErrors.Add($"Storage Account: {storageOptions.AccountName}");
        }
        else
        {
            runtimeErrors.Add("Connection string authentication fejlede. Kontrollér at connection string er korrekt.");
        }
    }
    else if (actualException.InnerException?.Message.Contains("authorization", StringComparison.OrdinalIgnoreCase) == true ||
             actualException.Message.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
             actualException.Message.Contains("403", StringComparison.OrdinalIgnoreCase))
    {
        runtimeErrors.Add("Adgang nægtet til Azure Storage. Kontrollér at brugeren/identity har de nødvendige rettigheder.");
        runtimeErrors.Add("Påkrævede roller: Storage Blob Data Contributor eller Storage Blob Data Owner");
    }
    else if (actualException.InnerException?.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) == true ||
             actualException.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
             actualException.Message.Contains("404", StringComparison.OrdinalIgnoreCase))
    {
        runtimeErrors.Add($"Storage account '{storageOptions.AccountName}' blev ikke fundet.");
        runtimeErrors.Add("Kontrollér at storage account navnet er korrekt i konfigurationen.");
    }
    else
    {
        runtimeErrors.Add($"Uventet fejl ved initialisering af storage: {actualException.Message}");
        if (actualException.InnerException != null)
        {
            runtimeErrors.Add($"Detaljer: {actualException.InnerException.Message}");
        }
    }

    // Setup middleware to show error page for all requests
    app.Use(async (HttpContext context, RequestDelegate next) =>
    {
        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Runtime-fejl</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            margin: 0;
            padding: 20px;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }}
        .container {{
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            max-width: 800px;
            width: 100%;
            padding: 40px;
        }}
        h1 {{
            color: #dc3545;
            margin-top: 0;
            font-size: 2em;
            display: flex;
            align-items: center;
            gap: 12px;
        }}
        .icon {{
            font-size: 1.2em;
        }}
        .error-list {{
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            border-radius: 8px;
            padding: 20px;
            margin: 20px 0;
        }}
        .error-list h2 {{
            margin-top: 0;
            color: #721c24;
            font-size: 1.2em;
        }}
        .error-list ul {{
            margin: 10px 0;
            padding-left: 20px;
        }}
        .error-list li {{
            color: #721c24;
            margin: 8px 0;
            line-height: 1.6;
        }}
        .help-section {{
            background: #e7f3ff;
            border: 1px solid #2196F3;
            border-radius: 8px;
            padding: 20px;
            margin-top: 20px;
        }}
        .help-section h2 {{
            margin-top: 0;
            color: #0c5460;
            font-size: 1.1em;
        }}
        .help-section p {{
            margin: 10px 0;
            color: #004085;
            line-height: 1.6;
        }}
        code {{
            background: #f8f9fa;
            padding: 2px 6px;
            border-radius: 3px;
            font-family: 'Courier New', monospace;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>
            <span class=""icon"">❌</span>
            Runtime-fejl
        </h1>

        <p>SmartGallery kunne ikke initialiseres på grund af en runtime-fejl.</p>

        <div class=""error-list"">
            <h2>Fejl:</h2>
            <ul>
                {string.Join("", runtimeErrors.Select(e => $"<li>{e}</li>"))}
            </ul>
        </div>

        <div class=""help-section"">
            <h2>Mulige løsninger:</h2>
            <p><strong>For Managed Identity:</strong></p>
            <ul>
                <li>Kontrollér at applikationen kører med en identity der har adgang til storage kontoen</li>
                <li>Giv identityen rollen ""Storage Blob Data Contributor"" på storage kontoen</li>
                <li>Vent et par minutter efter at have tildelt rollen</li>
            </ul>

            <p><strong>For Connection String:</strong></p>
            <ul>
                <li>Kontrollér at connection string er korrekt kopieret fra Azure Portal</li>
                <li>Kontrollér at storage kontoen eksisterer og er tilgængelig</li>
                <li>Test connection string i Azure Storage Explorer</li>
            </ul>
        </div>
    </div>
</body>
</html>";
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(html);
    });

    await app.RunAsync();
    return;
}

// If we get here, service initialization succeeded
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();