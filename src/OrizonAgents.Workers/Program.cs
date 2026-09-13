using OrizonAgents.Infrastructure;
using OrizonAgents.Infrastructure.Billing;
using OrizonAgents.Workers;
using OrizonAgents.Infrastructure.Health;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration, addWebSecurity: false);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await DatabaseGuardStartup.ValidateAsync(host.Services, host.Services.GetRequiredService<IHostEnvironment>());
await BillingSeeder.SeedAsync(host.Services);
host.Run();
