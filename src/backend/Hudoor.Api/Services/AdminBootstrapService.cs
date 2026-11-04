using Hudoor.Api.Data;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hudoor.Api.Services;

public class AdminBootstrapService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminBootstrapService> _logger;

    public AdminBootstrapService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AdminBootstrapService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        try
        {
            // 1. If any PlatformAdmin account already exists, do not modify, replace, or duplicate anything.
            if (await context.PlatformAdmins.AnyAsync(cancellationToken))
            {
                return;
            }

            // 2. Read credentials ONLY from Environment Variables, User Secrets, or securely injected config.
            // NO default username or password fallbacks.
            var username = _configuration["Bootstrap:AdminUsername"]
                ?? Environment.GetEnvironmentVariable("HUDOOR_ADMIN_BOOTSTRAP_USERNAME");

            var password = _configuration["Bootstrap:AdminPassword"]
                ?? Environment.GetEnvironmentVariable("HUDOOR_ADMIN_BOOTSTRAP_PASSWORD");

            // 3. If credentials are missing, do not create any Admin and do not use a fallback.
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Bootstrap: PlatformAdmin bootstrap credentials were not provided. Initial admin account creation skipped.");
                return;
            }

            // 4. Validate password policy for staff account (min 8 chars, letters and digits)
            var (isValidPassword, passwordError) = passwordService.ValidateStaffPassword(password);
            if (!isValidPassword)
            {
                _logger.LogWarning("Bootstrap: Provided admin bootstrap password does not meet security policy: {Error}. Seeding aborted.", passwordError);
                return;
            }

            var email = _configuration["Bootstrap:AdminEmail"]
                ?? Environment.GetEnvironmentVariable("HUDOOR_ADMIN_BOOTSTRAP_EMAIL")
                ?? "admin@hudoor.local";

            var fullName = _configuration["Bootstrap:AdminFullName"]
                ?? Environment.GetEnvironmentVariable("HUDOOR_ADMIN_BOOTSTRAP_FULLNAME")
                ?? "Platform Administrator";

            var admin = new PlatformAdmin
            {
                Id = Guid.NewGuid(),
                Username = username.Trim(),
                PasswordHash = passwordService.HashPassword(password),
                Email = email.Trim().ToLowerInvariant(),
                FullName = fullName.Trim(),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            context.PlatformAdmins.Add(admin);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Bootstrap: Initial PlatformAdmin account provisioned successfully for '{Username}'.", admin.Username);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bootstrap: An error occurred while checking or provisioning PlatformAdmin.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
