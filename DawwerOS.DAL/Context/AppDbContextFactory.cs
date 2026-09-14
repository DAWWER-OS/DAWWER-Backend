using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DawwerOS.DAL.Context;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var webApiPath = Path.GetFullPath(Path.Combine(basePath, "..", "DawwerOS.WebApi"));

        var builder = new ConfigurationBuilder();

        if (Directory.Exists(webApiPath))
        {
            builder.SetBasePath(webApiPath);
        }
        else
        {
            builder.SetBasePath(basePath);
        }

        builder.AddJsonFile("appsettings.json", optional: true)
               .AddJsonFile("appsettings.Development.json", optional: true);

        var configuration = builder.Build();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=DawwerOSDb;Username=postgres;Password=123123a";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
            npgsqlOptions.CommandTimeout(30);
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
        });
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options);
    }
}
