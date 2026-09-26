using FordNexus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FordNexus.Api.Tests.Infrastructure;

public sealed class NexusApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"FordNexusTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<NexusDbContext>>();
            services.AddDbContext<NexusDbContext>(o => o.UseInMemoryDatabase(_databaseName));
        });
    }
}
