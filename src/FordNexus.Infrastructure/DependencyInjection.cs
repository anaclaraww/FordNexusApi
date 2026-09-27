using FordNexus.Application.Abstractions;
using FordNexus.Infrastructure.Mqtt;
using FordNexus.Infrastructure.Observability;
using FordNexus.Infrastructure.Persistence;
using FordNexus.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FordNexus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool allowEphemeralSecrets)
    {
        var databaseName = configuration["Database:Name"] ?? "FordNexusDb";
        services.AddDbContext<NexusDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<INexusDbContext>(sp => sp.GetRequiredService<NexusDbContext>());

        // fora de produção, sem chave configurada, gera uma aleatória na subida: nunca existe chave fixa no repositório
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .PostConfigure(o =>
            {
                if (allowEphemeralSecrets && string.IsNullOrWhiteSpace(o.SigningKey))
                    o.SigningKey = EphemeralSecrets.JwtSigningKey;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<EncryptionOptions>()
            .Bind(configuration.GetSection(EncryptionOptions.SectionName))
            .PostConfigure(o =>
            {
                if (allowEphemeralSecrets && string.IsNullOrWhiteSpace(o.Key))
                    o.Key = EphemeralSecrets.EncryptionKey;
            })
            .ValidateDataAnnotations()
            .Validate(o => o.IsValid(), "Encryption:Key precisa ser uma chave de 32 bytes em Base64.")
            .ValidateOnStart();

        services.AddSingleton<INexusMetrics, PrometheusMetrics>();

        services.AddOptions<MqttOptions>().Bind(configuration.GetSection(MqttOptions.SectionName));
        if (configuration.GetValue<bool>($"{MqttOptions.SectionName}:{nameof(MqttOptions.Enabled)}"))
            services.AddHostedService<MqttTelemetryListener>();

        services.AddSingleton<IFieldEncryptor, AesGcmFieldEncryptor>();
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
