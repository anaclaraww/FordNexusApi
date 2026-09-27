using FordNexus.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FordNexus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IWorkshopService, WorkshopService>();
        services.AddScoped<IDealershipService, DealershipService>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<ITelemetryService, TelemetryService>();
        return services;
    }
}
