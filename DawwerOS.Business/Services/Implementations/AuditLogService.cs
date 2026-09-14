using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Audit;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public AuditLogService(
        AppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<AuditLog> LogAsync(
        string action,
        string entityType,
        string entityId,
        Guid? userId = null,
        Guid? storeId = null,
        object? oldValue = null,
        object? newValue = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve actor user identifier if not explicitly passed
        var resolvedUserId = userId ?? GetCurrentUserIdFromContext();

        // 2. Resolve client IP address if not explicitly passed
        var resolvedIpAddress = ipAddress ?? GetCurrentIpAddressFromContext();

        // 3. Serialize values cleanly, stripping secret or binary fields
        var serializedOldValue = SerializeValue(oldValue);
        var serializedNewValue = SerializeValue(newValue);

        var auditLog = new AuditLog
        {
            UserId = resolvedUserId,
            StoreId = storeId,
            Action = action.Trim().ToUpperInvariant(),
            EntityType = entityType.Trim(),
            EntityId = entityId.Trim(),
            OldValue = serializedOldValue,
            NewValue = serializedNewValue,
            IpAddress = resolvedIpAddress,
            Timestamp = DateTime.UtcNow
        };

        await _dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Audit event recorded: Action={Action}, EntityType={EntityType}, EntityId={EntityId}, ActorUserId={UserId}, StoreId={StoreId}",
            auditLog.Action, auditLog.EntityType, auditLog.EntityId, auditLog.UserId, auditLog.StoreId);

        return auditLog;
    }

    public async Task<ApiResponse<PagedAuditLogsResponseDto>> GetAuditLogsAsync(
        AuditLogFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Store)
            .AsQueryable();

        // Filter by Actor User
        if (filter.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == filter.UserId.Value);
        }

        // Filter by Store Context
        if (filter.StoreId.HasValue)
        {
            query = query.Where(a => a.StoreId == filter.StoreId.Value);
        }

        // Filter by Action code
        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var actionNormalized = filter.Action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action.ToUpper() == actionNormalized);
        }

        // Filter by Entity Type
        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            var entityTypeNormalized = filter.EntityType.Trim().ToLowerInvariant();
            query = query.Where(a => a.EntityType.ToLower() == entityTypeNormalized);
        }

        // Filter by Date Range (FromDate)
        if (filter.FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.FromDate.Value, DateTimeKind.Utc);
            query = query.Where(a => a.Timestamp >= fromUtc);
        }

        // Filter by Date Range (ToDate)
        if (filter.ToDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(filter.ToDate.Value, DateTimeKind.Utc);
            query = query.Where(a => a.Timestamp <= toUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogResponseDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserFullName = a.User != null ? a.User.FullName : null,
                UserEmail = a.User != null ? a.User.Email : null,
                StoreId = a.StoreId,
                StoreName = a.Store != null ? a.Store.Name : null,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                IpAddress = a.IpAddress,
                Timestamp = a.Timestamp
            })
            .ToListAsync(cancellationToken);

        var pagedResult = new PagedAuditLogsResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return ApiResponse<PagedAuditLogsResponseDto>.Ok(pagedResult, "Audit logs retrieved successfully.");
    }

    public async Task<ApiResponse<AuditLogResponseDto>> GetAuditLogByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var log = await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Store)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (log == null)
        {
            return ApiResponse<AuditLogResponseDto>.Fail("Audit log entry not found.");
        }

        var dto = new AuditLogResponseDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserFullName = log.User?.FullName,
            UserEmail = log.User?.Email,
            StoreId = log.StoreId,
            StoreName = log.Store?.Name,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            OldValue = log.OldValue,
            NewValue = log.NewValue,
            IpAddress = log.IpAddress,
            Timestamp = log.Timestamp
        };

        return ApiResponse<AuditLogResponseDto>.Ok(dto);
    }

    private Guid? GetCurrentUserIdFromContext()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return null;

        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? user.FindFirstValue("sub");

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private string? GetCurrentIpAddressFromContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return null;

        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var clientIp = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(clientIp))
            {
                return clientIp;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? SerializeValue(object? value)
    {
        if (value == null) return null;
        if (value is string s) return s;

        try
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
        catch
        {
            return value.ToString();
        }
    }
}
