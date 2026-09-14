using DawwerOS.DAL.Context;
using DawwerOS.DAL.Repositories.Implementations;
using DawwerOS.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DawwerOS.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found. Please configure it in appsettings.json or environment variables.");
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(30);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            });

            options.UseSnakeCaseNamingConvention();
            options.EnableDetailedErrors();
        });

        services.AddScoped(
            typeof(IGenericRepository<>),
            typeof(GenericRepository<>));

        return services;
    }
}
