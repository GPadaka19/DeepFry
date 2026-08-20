using LabManagement.Client;
using Microsoft.Extensions.Hosting.WindowsServices;

bool isWindowsService = WindowsServiceHelpers.IsWindowsService();
bool allowInteractiveRun = args.Contains(
    "--console",
    StringComparer.OrdinalIgnoreCase) ||
    System.Diagnostics.Debugger.IsAttached;

if (!isWindowsService && !allowInteractiveRun)
{
    Console.Error.WriteLine(
        "LabManagement.Client harus dijalankan sebagai Windows Service. " +
        "Gunakan --console hanya untuk pengujian teknis.");
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "LabManagement Client";
});
builder.Services.AddSingleton<ClientSharedSecretProvider>();
builder.Services.AddSingleton<IUwfManager, UwfManager>();
builder.Services.AddSingleton<IClientCommandDispatcher, ClientCommandDispatcher>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
