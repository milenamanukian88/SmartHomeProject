using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("devices", client =>
{
    client.Timeout = TimeSpan.FromSeconds(3);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Only for the local development device emulators.
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var devices = new[]
{
    new { Name = "Device1", Url = "https://localhost:1234/info" },
    new { Name = "Device2", Url = "https://localhost:1236/info" }
};

app.MapGet("/api/devices", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("devices");

    var results = await Task.WhenAll(devices.Select(async device =>
    {
        try
        {
            using var response = await client.GetAsync(device.Url);
            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            var root = json.RootElement;

            var metrics = root.GetProperty("metrics");

            return new
            {
                name = device.Name,
                status = "Online",
                temperature = metrics.GetProperty("Temperature").GetString(),
                uptime = metrics.GetProperty("UptimeSeconds").GetString()
            };
        }
        catch
        {
            return new
            {
                name = device.Name,
                status = "Offline",
                temperature = (string?)null,
                uptime = (string?)null
            };
        }
    }));

    return Results.Ok(new
    {
        totalDevices = results.Length,
        onlineDevices = results.Count(d => d.status == "Online"),
        offlineDevices = results.Count(d => d.status == "Offline"),
        devices = results
    });
});

app.Run();
