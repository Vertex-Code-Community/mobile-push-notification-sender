using PushSharp.Net;
using PushSharp.Net.DependencyInjection;
using PushSharp.Net.Registration;
using PushSharp.Net.Sandbox.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddPushNotifications((push) =>
{
    push.AddFcm(fcm =>
    {
        fcm.ProjectId = builder.Configuration["Fcm:ProjectId"] ?? "farmapp-c6c53";
        var configuredPath = builder.Configuration["Fcm:CredentialFilePath"];
        fcm.CredentialFilePath = string.IsNullOrWhiteSpace(configuredPath)
            ? ResolveCredentialPath(builder.Environment)
            : configuredPath;
    });
    push.AddDeviceStore(new InMemoryDeviceRegistrationRepository());
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string ResolveCredentialPath(IWebHostEnvironment env)
{
    foreach (var root in new[] { env.ContentRootPath, Directory.GetCurrentDirectory() })
    {
        foreach (var rel in new[] { "firebase-service-account.json", Path.Combine("..", "..", "firebase-service-account.json") })
        {
            var full = Path.GetFullPath(Path.Combine(root, rel));
            if (File.Exists(full))
                return full;
        }
    }

    return Path.Combine(env.ContentRootPath, "firebase-service-account.json");
}
