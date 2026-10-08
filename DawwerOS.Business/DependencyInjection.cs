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
            services.Configure<JwtSettings>(settings =>
            {
                configuration.GetSection(JwtSettings.SectionName).Bind(settings);

                var envSecretKey = configuration["JWT_SECRET_KEY"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
                if (!string.IsNullOrWhiteSpace(envSecretKey)) settings.Key = envSecretKey;

                var envIssuer = configuration["JWT_ISSUER"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER");
                if (!string.IsNullOrWhiteSpace(envIssuer)) settings.Issuer = envIssuer;

                var envAudience = configuration["JWT_AUDIENCE"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE");
                if (!string.IsNullOrWhiteSpace(envAudience)) settings.Audience = envAudience;

                var envAlgorithm = configuration["JWT_ALGORITHM"] ?? Environment.GetEnvironmentVariable("JWT_ALGORITHM");
                if (!string.IsNullOrWhiteSpace(envAlgorithm)) settings.Algorithm = envAlgorithm;
            });
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
        services.AddSingleton<IOrderStateMachine, OrderStateMachine>();
        services.AddScoped<IStockHoldService, StockHoldService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IDeliveryZoneService, DeliveryZoneService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentReviewService, PaymentReviewService>();
        services.AddScoped<IRefundService, RefundService>();

        return services;
    }
}
