using Hudoor.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hudoor.Api.Tests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "HudoorTestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "HudoorTestSuperSecretSigningKey2026!#*LongEnoughForHmacSha256",
                ["Jwt:Issuer"] = "Hudoor.Api",
                ["Jwt:Audience"] = "Hudoor.Client",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Bootstrap:AdminUsername"] = "admin",
                ["Bootstrap:AdminPassword"] = "AdminSecure2026!",
                ["Bootstrap:AdminEmail"] = "admin@hudoor.local",
                ["Bootstrap:AdminFullName"] = "Platform Administrator"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }

    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
