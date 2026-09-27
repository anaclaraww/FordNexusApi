using FordNexus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FordNexus.Api.Tests.Infrastructure;

public class NexusApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"FordNexusTests-{Guid.NewGuid()}";

    protected virtual string Environment => "Testing";
    protected virtual bool RelaxRateLimits => true;

    protected virtual void ConfigureSettings(IWebHostBuilder builder) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);

        if (RelaxRateLimits)
        {
            builder.UseSetting("RateLimiting:LoginPermitLimit", "100000");
            builder.UseSetting("RateLimiting:ApiPermitLimit", "100000");
        }

        ConfigureSettings(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<NexusDbContext>>();
            services.AddDbContext<NexusDbContext>(o => o.UseInMemoryDatabase(_databaseName));
        });
    }
}

public sealed class StrictRateLimitApiFactory : NexusApiFactory
{
    protected override bool RelaxRateLimits => false;
}

public sealed class ProductionApiFactory : NexusApiFactory
{
    public static readonly string SigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
    public static readonly string EncryptionKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    protected override string Environment => "Production";

    protected override void ConfigureSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Encryption:Key", EncryptionKey);
    }
}

public sealed class ProductionWithoutSecretsApiFactory : NexusApiFactory
{
    protected override string Environment => "Production";
}
