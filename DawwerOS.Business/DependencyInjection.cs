using DawwerOS.Business.Common.Settings;
using DawwerOS.Business.Services.Implementations;
using DawwerOS.Business.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DawwerOS.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusiness(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration != null)
        {
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        }

        services.AddMemoryCache();

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<ITokenRevocationService, TokenRevocationService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMerchantStoreService, MerchantStoreService>();
        services.AddScoped<IAdminStoreService, AdminStoreService>();
        services.AddScoped<IPublicStoreService, PublicStoreService>();
        services.AddScoped<IStoreAuthorizationService, StoreAuthorizationService>();
        services.AddScoped<IStoreStaffService, StoreStaffService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAdminUserService, AdminUserService>();

        return services;
    }
}
