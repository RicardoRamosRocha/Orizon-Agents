using Microsoft.AspNetCore.Authentication;
using OrizonAgents.Infrastructure;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.API.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            AgentApiKeyDefaults.AuthenticationScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, AgentApiKeyAuthenticationHandler>(
        AgentApiKeyDefaults.AuthenticationScheme,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AgentApiKeyDefaults.AuthorizationPolicy,
        policy =>
        {
            policy.AddAuthenticationSchemes(
                AgentApiKeyDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
        });
});
builder.Services.AddAgentApiRateLimiting();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseCurrentTenant();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
