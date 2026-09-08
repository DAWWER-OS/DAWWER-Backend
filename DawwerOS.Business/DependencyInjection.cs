using Microsoft.Extensions.DependencyInjection;

namespace DawwerOS.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusiness(this IServiceCollection services)
    {
        // Register your domain services here as you add them.
        // Example:
        // services.AddScoped<IYourService, YourService>();

        return services;
    }
}
