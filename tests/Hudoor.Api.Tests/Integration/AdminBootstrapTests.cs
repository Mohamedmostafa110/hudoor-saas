using Hudoor.Api.Data;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hudoor.Api.Tests.Integration;

public class ParameterizedWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName;
    private readonly Dictionary<string, string?> _config;

    public ParameterizedWebApplicationFactory(string dbName, Dictionary<string, string?> config)
    {
        _dbName = dbName;
        _config = config;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(_config);
        });

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
        });
    }
}

public class AdminBootstrapTests
{
    [Fact]
    public async Task Bootstrap_WithValidCredentials_AndEmptyTable_CreatesAdminOnce()
    {
        var dbName = "BootstrapTestDb_" + Guid.NewGuid();
        var config = new Dictionary<string, string?>
        {
            ["TestDbName"] = dbName,
            ["Jwt:Key"] = "HudoorTestSuperSecretSigningKey2026!#*LongEnoughForHmacSha256",
            ["Bootstrap:AdminUsername"] = "custom_bootstrap_admin",
            ["Bootstrap:AdminPassword"] = "BootstrapPass2026!",
            ["Bootstrap:AdminEmail"] = "bootstrap@hudoor.local",
            ["Bootstrap:AdminFullName"] = "Bootstrap Admin"
        };

        using var factory = new ParameterizedWebApplicationFactory(dbName, config);
        _ = factory.CreateClient(); // Triggers host startup and AdminBootstrapService

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pwdService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var admins = await db.PlatformAdmins.ToListAsync();
        Assert.Single(admins);
        var admin = admins[0];
        Assert.Equal("custom_bootstrap_admin", admin.Username);
        Assert.Equal("bootstrap@hudoor.local", admin.Email);
        Assert.True(pwdService.VerifyPassword(admin.PasswordHash, "BootstrapPass2026!"));
    }

    [Fact]
    public async Task Bootstrap_WithoutCredentials_AndEmptyTable_DoesNotCreateAdmin()
    {
        var dbName = "BootstrapEmptyTestDb_" + Guid.NewGuid();
        var config = new Dictionary<string, string?>
        {
            ["TestDbName"] = dbName,
            ["Jwt:Key"] = "HudoorTestSuperSecretSigningKey2026!#*LongEnoughForHmacSha256"
            // Intentionally omitting Bootstrap:AdminUsername and Bootstrap:AdminPassword
        };

        using var factory = new ParameterizedWebApplicationFactory(dbName, config);
        _ = factory.CreateClient(); // Triggers host startup

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var admins = await db.PlatformAdmins.ToListAsync();
        Assert.Empty(admins); // Absolutely NO admin created!
    }

    [Fact]
    public async Task Bootstrap_WhenAdminAlreadyExists_DoesNotReplaceOrModifyAdmin()
    {
        var dbName = "BootstrapExistingTestDb_" + Guid.NewGuid();

        // 1. First, seed an existing admin directly into this DB
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var pwdService = new PasswordService();

        using (var initialDb = new ApplicationDbContext(options))
        {
            initialDb.PlatformAdmins.Add(new PlatformAdmin
            {
                Id = Guid.NewGuid(),
                Username = "original_admin",
                PasswordHash = pwdService.HashPassword("OriginalPass123!"),
                FullName = "Original Administrator",
                Email = "original@hudoor.local",
                IsActive = true
            });
            await initialDb.SaveChangesAsync();
        }

        // 2. Start application with DIFFERENT bootstrap credentials
        var config = new Dictionary<string, string?>
        {
            ["TestDbName"] = dbName,
            ["Jwt:Key"] = "HudoorTestSuperSecretSigningKey2026!#*LongEnoughForHmacSha256",
            ["Bootstrap:AdminUsername"] = "new_attempted_admin",
            ["Bootstrap:AdminPassword"] = "NewAttemptedPass456!",
            ["Bootstrap:AdminEmail"] = "new@hudoor.local"
        };

        using var factory = new ParameterizedWebApplicationFactory(dbName, config);
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Verify: Still only 1 admin, existing admin was untouched
        var admins = await db.PlatformAdmins.ToListAsync();
        Assert.Single(admins);
        var admin = admins[0];
        Assert.Equal("original_admin", admin.Username);
        Assert.Equal("original@hudoor.local", admin.Email);
        Assert.True(pwdService.VerifyPassword(admin.PasswordHash, "OriginalPass123!"));
        Assert.False(pwdService.VerifyPassword(admin.PasswordHash, "NewAttemptedPass456!"));
    }

    [Fact]
    public async Task Bootstrap_OnAppRestart_DoesNotDuplicateAccount()
    {
        var dbName = "BootstrapRestartTestDb_" + Guid.NewGuid();
        var config = new Dictionary<string, string?>
        {
            ["TestDbName"] = dbName,
            ["Jwt:Key"] = "HudoorTestSuperSecretSigningKey2026!#*LongEnoughForHmacSha256",
            ["Bootstrap:AdminUsername"] = "restart_admin",
            ["Bootstrap:AdminPassword"] = "RestartPass2026!"
        };

        // First startup: creates restart_admin
        using (var factory1 = new ParameterizedWebApplicationFactory(dbName, config))
        {
            _ = factory1.CreateClient();
            using var scope1 = factory1.Services.CreateScope();
            var db1 = scope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Single(await db1.PlatformAdmins.ToListAsync());
        }

        // Second startup (simulating restart against same database)
        using (var factory2 = new ParameterizedWebApplicationFactory(dbName, config))
        {
            _ = factory2.CreateClient();
            using var scope2 = factory2.Services.CreateScope();
            var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var admins = await db2.PlatformAdmins.ToListAsync();
            Assert.Single(admins);
            Assert.Equal("restart_admin", admins[0].Username);
        }
    }
}
