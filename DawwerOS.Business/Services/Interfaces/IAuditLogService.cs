using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Audit;
using DawwerOS.DAL.Entities;

namespace DawwerOS.Business.Services.Interfaces;

public interface IAuditLogService
{
    Task<AuditLog> LogAsync(
        string action,
        string entityType,
        string entityId,
        Guid? userId = null,
        Guid? storeId = null,
        object? oldValue = null,
        object? newValue = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PagedAuditLogsResponseDto>> GetAuditLogsAsync(
        AuditLogFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AuditLogResponseDto>> GetAuditLogByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
