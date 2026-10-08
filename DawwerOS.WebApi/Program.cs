using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using DawwerOS.Business;
using DawwerOS.Business.Common;
using DawwerOS.Business.Common.Settings;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using DawwerOS.DAL.Repositories.Interfaces;
using DawwerOS.WebApi.Middleware;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;


using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers with standardized ApiResponse validation error envelope
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e =>
                    !string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? e.ErrorMessage
                        : $"{x.Key}: Invalid value."))
                .ToList();

            var response = ApiResponse<object>.Fail("Validation failed.", errors);
            return new BadRequestObjectResult(response);
        };
    });

// Configure CORS: Allow all websites / origins, methods, headers, and credentials
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add HttpContextAccessor for request context and IP resolution in audit logs
builder.Services.AddHttpContextAccessor();

// Add API Explorer & Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DawwerOS API",
        Version = "v1",
        Description = "DawwerOS ASP.NET Core Web API using PostgreSQL and clean layered architecture"
    });

    // XML Documentation comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // JWT Bearer Swagger Security Definition
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token only (do not include 'Bearer ' prefix)"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

var envSecretKey = builder.Configuration["JWT_SECRET_KEY"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (!string.IsNullOrWhiteSpace(envSecretKey))
{
    jwtSettings.Key = envSecretKey;
}

var envIssuer = builder.Configuration["JWT_ISSUER"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER");
if (!string.IsNullOrWhiteSpace(envIssuer))
{
    jwtSettings.Issuer = envIssuer;
}

var envAudience = builder.Configuration["JWT_AUDIENCE"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE");
if (!string.IsNullOrWhiteSpace(envAudience))
{
    jwtSettings.Audience = envAudience;
}

var envAlgorithm = builder.Configuration["JWT_ALGORITHM"] ?? Environment.GetEnvironmentVariable("JWT_ALGORITHM");
if (!string.IsNullOrWhiteSpace(envAlgorithm))
{
    jwtSettings.Algorithm = envAlgorithm;
}

var key = Encoding.UTF8.GetBytes(jwtSettings.Key);
var expectedAlgorithm = jwtSettings.Algorithm?.Trim().ToUpperInvariant() switch
{
    "HS384" => SecurityAlgorithms.HmacSha384,
    "HS512" => SecurityAlgorithms.HmacSha512,
    _ => SecurityAlgorithms.HmacSha256
};

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        ValidAlgorithms = new[] { expectedAlgorithm }
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var services = context.HttpContext.RequestServices;
            var tokenRevocationService = services.GetRequiredService<ITokenRevocationService>();

            // 1. Check if specific JWT JTI has been revoked
            var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
                      ?? context.Principal?.FindFirst("jti")?.Value;

            if (!string.IsNullOrEmpty(jti) && await tokenRevocationService.IsTokenRevokedAsync(jti))
            {
                context.Fail("This token has been revoked.");
                return;
            }

            // 2. Reject requests and invalidate active sessions for suspended or inactive users
            var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? context.Principal?.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                var userRepository = services.GetRequiredService<IGenericRepository<User>>();
                var user = await userRepository.GetByIdAsync(userId);

                if (user == null || user.Status == UserStatus.Suspended || user.Status == UserStatus.Inactive)
                {
                    context.Fail("Your account is inactive or has been suspended.");
                    return;
                }
            }
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var errorMsg = context.AuthenticateFailure?.Message;
            var message = !string.IsNullOrWhiteSpace(errorMsg)
                ? errorMsg
                : "Unauthorized access. Token is invalid, expired, or revoked.";

            var response = ApiResponse<object>.Fail(message, new[] { "Authentication required." });
            var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail("Forbidden. You do not have permission to access this resource.");
            var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    };
});

builder.Services.AddAuthorization();

// Configure Authentication Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var response = ApiResponse<object>.Fail("Too many requests. Please slow down and try again later.");
        var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        await context.HttpContext.Response.WriteAsync(json, token);
    };

    options.AddPolicy("AuthRateLimit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 200 : 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

// Configure Hangfire with PostgreSQL Storage
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

// Add Layer Dependencies
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBusiness(builder.Configuration);

var app = builder.Build();

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionMiddleware>();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "DawwerOS API v1");
    options.RoutePrefix = "swagger";
});

// Enable CORS
app.UseCors();

app.UseHttpsRedirection();

// Rate Limiter
app.UseRateLimiter();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();


// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire");

// Root redirect to Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapControllers();

// Apply pending EF Core migrations automatically on startup and seed Super Admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        logger.LogInformation("Checking and applying pending database migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");

        await DbInitializer.SeedSuperAdminAsync(services);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "An error occurred while migrating or seeding the database on startup. Application will continue starting, but database operations may fail.");
    }
}

app.Run();

public partial class Program { }
