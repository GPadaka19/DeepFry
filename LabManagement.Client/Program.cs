using LabManagement.Client;

ClientConsoleBranding.Print();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "LabManagement Client";
});
builder.Services.AddSingleton<IUwfManager, UwfManager>();
builder.Services.AddSingleton<ISystemPowerManager, SystemPowerManager>();
builder.Services.AddSingleton<IClientCommandDispatcher, ClientCommandDispatcher>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
