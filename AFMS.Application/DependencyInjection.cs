using AFMS.Application.Abstractions;
using AFMS.Application.Services;
using AFMS.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AFMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<SerialRangeService>();
        services.AddSingleton<ICurrentUserSession, CurrentUserSession>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IFirstRunService, FirstRunService>();
        return services;
    }
}
