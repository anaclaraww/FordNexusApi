using FordNexus.Application.Abstractions;
using FordNexus.Infrastructure.Persistence;
using FordNexus.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FordNexus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseName = configuration["Database:Name"] ?? "FordNexusDb";
        services.AddDbContext<NexusDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<INexusDbContext>(sp => sp.GetRequiredService<NexusDbContext>());

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        return services;
    }

    public static async Task SeedDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        await SeedData.SeedAsync(
            sp.GetRequiredService<NexusDbContext>(),
            sp.GetRequiredService<IPasswordHasher>(),
            sp.GetRequiredService<TimeProvider>(),
            ct);
    }
}
