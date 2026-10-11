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

            services.Configure<EmailSettings>(settings =>
            {
                configuration.GetSection(EmailSettings.SectionName).Bind(settings);

                var envSmtpHost = configuration["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
                if (!string.IsNullOrWhiteSpace(envSmtpHost)) settings.SmtpHost = envSmtpHost;

                var envSmtpPort = configuration["SMTP_PORT"] ?? Environment.GetEnvironmentVariable("SMTP_PORT");
                if (!string.IsNullOrWhiteSpace(envSmtpPort) && int.TryParse(envSmtpPort, out var port)) settings.SmtpPort = port;

                var envSmtpUser = configuration["SMTP_USERNAME"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
                if (!string.IsNullOrWhiteSpace(envSmtpUser)) settings.SmtpUsername = envSmtpUser;

                var envSmtpPass = configuration["SMTP_PASSWORD"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
                if (!string.IsNullOrWhiteSpace(envSmtpPass)) settings.SmtpPassword = envSmtpPass;

                var envSenderEmail = configuration["SMTP_SENDER_EMAIL"] ?? configuration["SMTP_FROM_EMAIL"] ?? Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL") ?? Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL");
                if (!string.IsNullOrWhiteSpace(envSenderEmail)) settings.SenderEmail = envSenderEmail;

                var envSenderName = configuration["SMTP_SENDER_NAME"] ?? configuration["SMTP_FROM_NAME"] ?? Environment.GetEnvironmentVariable("SMTP_SENDER_NAME") ?? Environment.GetEnvironmentVariable("SMTP_FROM_NAME");
                if (!string.IsNullOrWhiteSpace(envSenderName)) settings.SenderName = envSenderName;

                var envEnableSsl = configuration["SMTP_ENABLE_SSL"] ?? Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL");
                if (!string.IsNullOrWhiteSpace(envEnableSsl) && bool.TryParse(envEnableSsl, out var enableSsl)) settings.EnableSsl = enableSsl;
            });
        }

        services.AddMemoryCache();

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<ITokenRevocationService, TokenRevocationService>();
        services.AddScoped<IEmailService, EmailService>();
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
