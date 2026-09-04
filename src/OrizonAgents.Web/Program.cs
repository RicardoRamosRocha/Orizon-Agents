using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using OrizonAgents.Infrastructure;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Billing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    // Keep loopback defaults; trust additional proxies only when explicitly configured.
    foreach (string proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }
});
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Resolve the original scheme from trusted proxies before HTTPS redirects and OAuth URL generation.
app.UseForwardedHeaders();

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

app.UseAuthentication();
app.UseCurrentTenant();
app.UseTenantSuspension();
app.UseAuthorization();

await IdentitySeeder.SeedAsync(app.Services);
await BillingSeeder.SeedAsync(app.Services);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
