using FordNexus.Domain.Common;
using Microsoft.AspNetCore.Authorization;

namespace FordNexus.Api.Auth;

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);

    public const string DealerNetwork = nameof(DealerNetwork);

    public const string ServiceNetwork = nameof(ServiceNetwork);

    public const string ServiceProviders = nameof(ServiceProviders);

    public const string HistoryReaders = nameof(HistoryReaders);

    public static AuthorizationBuilder AddNexusPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(AdminOnly, p => p.RequireRole(Roles.Admin))
        .AddPolicy(DealerNetwork, p => p.RequireRole(Roles.Admin, Roles.Dealer))
        .AddPolicy(ServiceNetwork, p => p.RequireRole(Roles.Admin, Roles.Dealer, Roles.Workshop))
        .AddPolicy(ServiceProviders, p => p.RequireRole(Roles.Dealer, Roles.Workshop))
        .AddPolicy(HistoryReaders, p => p.RequireRole(Roles.Admin, Roles.Dealer, Roles.Workshop, Roles.Partner));
}
