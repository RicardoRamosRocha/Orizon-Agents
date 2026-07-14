using OrizonAgents.Infrastructure;
using OrizonAgents.Infrastructure.Billing;
using OrizonAgents.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration, addWebSecurity: false);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await BillingSeeder.SeedAsync(host.Services);
host.Run();
