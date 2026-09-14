using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DawwerOS.WebApi.Authorization;

public class StorePermissionFilter : IAsyncActionFilter
{
    private readonly string? _requiredPermission;
    private readonly IStoreAuthorizationService _storeAuthorizationService;
    private readonly ILogger<StorePermissionFilter> _logger;

    public StorePermissionFilter(
        string requiredPermission,
        IStoreAuthorizationService storeAuthorizationService,
        ILogger<StorePermissionFilter> logger)
    {
        _requiredPermission = string.IsNullOrWhiteSpace(requiredPermission) ? null : requiredPermission;
        _storeAuthorizationService = storeAuthorizationService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // 1. Ensure caller is authenticated
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail("Unauthorized access."))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        // 2. Extract UserId and Role
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail("Unauthorized access: Invalid identity token."))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value
                        ?? user.FindFirst("role")?.Value;

        if (!Enum.TryParse<UserRole>(roleClaim, true, out var userRole))
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail("Forbidden: Unrecognized account role."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // 3. Extract requested StoreId from Route Data
        if (!context.RouteData.Values.TryGetValue("storeId", out var storeIdObj) ||
            !Guid.TryParse(storeIdObj?.ToString(), out var storeId))
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail("Invalid route: StoreId parameter is required."))
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
            return;
        }

        // 4. Validate requested StoreId against authenticated StoreId claim (Tenant Isolation)
        var tokenStoreIdClaim = user.FindFirst("store_id")?.Value
                                ?? user.FindFirst("StoreId")?.Value;

        if (!string.IsNullOrEmpty(tokenStoreIdClaim) &&
            Guid.TryParse(tokenStoreIdClaim, out var tokenStoreId) &&
            userRole != UserRole.Admin)
        {
            if (tokenStoreId != storeId)
            {
                _logger.LogWarning(
                    "Cross-store access violation rejected: Token store {TokenStoreId} != Requested route store {RouteStoreId} for user {UserId}",
                    tokenStoreId, storeId, userId);

                context.Result = new ObjectResult(
                    ApiResponse<object>.Fail(
                        "Forbidden: Cross-store data access is strictly prohibited.",
                        new[] { "Tenant isolation policy violation." }))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }

        // 5. Store-level membership & access check
        var hasAccess = await _storeAuthorizationService.HasStoreAccessAsync(
            userId, userRole, storeId, context.HttpContext.RequestAborted);

        if (!hasAccess)
        {
            _logger.LogWarning(
                "Access rejected: User {UserId} with role {Role} does not have access to store {StoreId}",
                userId, userRole, storeId);

            context.Result = new ObjectResult(
                ApiResponse<object>.Fail("Forbidden: You do not have access to this store."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // 6. Permission-based authorization policy check
        if (!string.IsNullOrEmpty(_requiredPermission))
        {
            var hasPerm = await _storeAuthorizationService.HasPermissionAsync(
                userId, userRole, storeId, _requiredPermission, context.HttpContext.RequestAborted);

            if (!hasPerm)
            {
                _logger.LogWarning(
                    "Permission denied: User {UserId} in store {StoreId} lacks required permission '{Permission}'",
                    userId, storeId, _requiredPermission);

                context.Result = new ObjectResult(
                    ApiResponse<object>.Fail(
                        $"Forbidden: You do not possess the required permission '{_requiredPermission}' for this store.",
                        new[] { "Insufficient privileges." }))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }

        // All authorization checks passed successfully
        await next();
    }
}
