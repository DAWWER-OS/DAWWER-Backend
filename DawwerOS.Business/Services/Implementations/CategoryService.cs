using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Category;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Services.Implementations;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        AppDbContext dbContext,
        IAuditLogService auditLogService,
        ILogger<CategoryService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ApiResponse<IEnumerable<CategoryResponseDto>>> GetCategoriesAsync(
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Categories
            .AsNoTracking()
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .AsQueryable();

        if (activeOnly == true)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLowerInvariant();
            query = query.Where(c => c.Name.ToLower().Contains(searchLower) ||
                                     (c.Description != null && c.Description.ToLower().Contains(searchLower)));
        }

        var categories = await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var result = categories.Select(MapToDto).ToList();
        return ApiResponse<IEnumerable<CategoryResponseDto>>.Ok(result);
    }

    public async Task<ApiResponse<IEnumerable<CategoryTreeResponseDto>>> GetCategoryTreeAsync(
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Categories
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly == true)
        {
            query = query.Where(c => c.IsActive);
        }

        var allCategories = await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        // Group by ParentCategoryId
        var lookup = allCategories.ToLookup(c => c.ParentCategoryId);

        // Build hierarchical tree starting from root categories
        var tree = lookup[null].Select(c => BuildTreeItem(c, lookup)).ToList();

        return ApiResponse<IEnumerable<CategoryTreeResponseDto>>.Ok(tree);
    }

    public async Task<ApiResponse<CategoryResponseDto>> GetCategoryByIdAsync(
        Guid id,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Categories
            .AsNoTracking()
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .AsQueryable();

        if (activeOnly == true)
        {
            query = query.Where(c => c.IsActive);
        }

        var category = await query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            return ApiResponse<CategoryResponseDto>.Fail("Category not found.");
        }

        return ApiResponse<CategoryResponseDto>.Ok(MapToDto(category));
    }

    public async Task<ApiResponse<CategoryResponseDto>> CreateCategoryAsync(
        CreateCategoryRequestDto request,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name.Trim();

        // 1. Prevent duplicate category names under the same parent
        var duplicateExists = await _dbContext.Categories.AnyAsync(
            c => c.ParentCategoryId == request.ParentCategoryId && c.Name.ToLower() == trimmedName.ToLower(),
            cancellationToken);

        if (duplicateExists)
        {
            return ApiResponse<CategoryResponseDto>.Fail(
                $"A category named '{trimmedName}' already exists at this level.",
                new[] { "Duplicate category names are not permitted at the same hierarchy level." });
        }

        // 2. Validate Parent Category if provided
        if (request.ParentCategoryId.HasValue)
        {
            var parent = await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ParentCategoryId.Value, cancellationToken);

            if (parent == null)
            {
                return ApiResponse<CategoryResponseDto>.Fail("Parent category not found.");
            }
        }

        var category = new Category
        {
            Name = trimmedName,
            Description = request.Description?.Trim(),
            IconUrl = request.IconUrl?.Trim(),
            ParentCategoryId = request.ParentCategoryId,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };

        await _dbContext.Categories.AddAsync(category, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Master category '{CategoryName}' (ID {CategoryId}) created by admin {AdminId}",
            category.Name, category.Id, adminUserId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.CategoryCreated,
            entityType: "Category",
            entityId: category.Id.ToString(),
            userId: adminUserId,
            newValue: new { name = category.Name, parentCategoryId = category.ParentCategoryId, isActive = category.IsActive, displayOrder = category.DisplayOrder },
            cancellationToken: cancellationToken);

        return await GetCategoryByIdAsync(category.Id, activeOnly: null, cancellationToken);
    }

    public async Task<ApiResponse<CategoryResponseDto>> UpdateCategoryAsync(
        Guid id,
        UpdateCategoryRequestDto request,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            return ApiResponse<CategoryResponseDto>.Fail("Category not found.");
        }

        var trimmedName = request.Name.Trim();

        // 1. Prevent duplicate category names
        var duplicateExists = await _dbContext.Categories.AnyAsync(
            c => c.Id != id && c.ParentCategoryId == request.ParentCategoryId && c.Name.ToLower() == trimmedName.ToLower(),
            cancellationToken);

        if (duplicateExists)
        {
            return ApiResponse<CategoryResponseDto>.Fail(
                $"Another category named '{trimmedName}' already exists at this level.",
                new[] { "Category names must be unique within the same hierarchy level." });
        }

        // 2. Hierarchy Cycle Detection (BR-15: Category cannot become its own ancestor)
        if (request.ParentCategoryId.HasValue)
        {
            if (request.ParentCategoryId.Value == id)
            {
                return ApiResponse<CategoryResponseDto>.Fail(
                    "A category cannot be its own parent.",
                    new[] { "Invalid parent category relationship." });
            }

            // Check if proposed parent is currently a descendant of this category
            var isCycle = await IsDescendantAsync(request.ParentCategoryId.Value, id, cancellationToken);
            if (isCycle)
            {
                return ApiResponse<CategoryResponseDto>.Fail(
                    "Hierarchy cycle detected: A category cannot have one of its descendants as its parent.",
                    new[] { "Acyclic hierarchy violation." });
            }

            var parentExists = await _dbContext.Categories.AnyAsync(
                c => c.Id == request.ParentCategoryId.Value,
                cancellationToken);

            if (!parentExists)
            {
                return ApiResponse<CategoryResponseDto>.Fail("Specified parent category not found.");
            }
        }

        var previousCategorySnapshot = new
        {
            name = category.Name,
            description = category.Description,
            iconUrl = category.IconUrl,
            parentCategoryId = category.ParentCategoryId,
            displayOrder = category.DisplayOrder
        };

        category.Name = trimmedName;
        category.Description = request.Description?.Trim();
        category.IconUrl = request.IconUrl?.Trim();
        category.ParentCategoryId = request.ParentCategoryId;
        category.DisplayOrder = request.DisplayOrder;
        category.UpdatedAt = DateTime.UtcNow;

        _dbContext.Categories.Update(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Master category '{CategoryName}' (ID {CategoryId}) updated by admin {AdminId}",
            category.Name, category.Id, adminUserId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.CategoryUpdated,
            entityType: "Category",
            entityId: category.Id.ToString(),
            userId: adminUserId,
            oldValue: previousCategorySnapshot,
            newValue: new { name = category.Name, description = category.Description, iconUrl = category.IconUrl, parentCategoryId = category.ParentCategoryId, displayOrder = category.DisplayOrder },
            cancellationToken: cancellationToken);

        return await GetCategoryByIdAsync(category.Id, activeOnly: null, cancellationToken);
    }

    public async Task<ApiResponse<bool>> DeleteCategoryAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            return ApiResponse<bool>.Fail("Category not found.");
        }

        // 1. Prevent invalid deletion when referenced by subcategories (Checklist item 15)
        if (category.SubCategories.Any())
        {
            return ApiResponse<bool>.Fail(
                $"Cannot delete category '{category.Name}' because it has {category.SubCategories.Count} subcategory/subcategories.",
                new[] { "Please reassign or delete subcategories first, or deactivate this category instead." });
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Master category '{CategoryName}' (ID {CategoryId}) deleted by admin {AdminId}",
            category.Name, id, adminUserId);

        return ApiResponse<bool>.Ok(true, $"Category '{category.Name}' deleted successfully.");
    }

    public async Task<ApiResponse<CategoryResponseDto>> ActivateCategoryAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            return ApiResponse<CategoryResponseDto>.Fail("Category not found.");
        }

        category.IsActive = true;
        category.UpdatedAt = DateTime.UtcNow;

        _dbContext.Categories.Update(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Master category '{CategoryName}' activated by admin {AdminId}", category.Name, adminUserId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.CategoryActivated,
            entityType: "Category",
            entityId: category.Id.ToString(),
            userId: adminUserId,
            oldValue: new { isActive = false },
            newValue: new { isActive = true },
            cancellationToken: cancellationToken);

        return await GetCategoryByIdAsync(category.Id, activeOnly: null, cancellationToken);
    }

    public async Task<ApiResponse<CategoryResponseDto>> DeactivateCategoryAsync(
        Guid id,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            return ApiResponse<CategoryResponseDto>.Fail("Category not found.");
        }

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;

        _dbContext.Categories.Update(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Master category '{CategoryName}' deactivated by admin {AdminId}", category.Name, adminUserId);

        // Record immutable audit entry
        await _auditLogService.LogAsync(
            action: AuditLogActions.CategoryDeactivated,
            entityType: "Category",
            entityId: category.Id.ToString(),
            userId: adminUserId,
            oldValue: new { isActive = true },
            newValue: new { isActive = false },
            cancellationToken: cancellationToken);

        return await GetCategoryByIdAsync(category.Id, activeOnly: null, cancellationToken);
    }

    private async Task<bool> IsDescendantAsync(Guid potentialDescendantId, Guid ancestorId, CancellationToken cancellationToken)
    {
        // Load all categories to walk the in-memory tree safely without N+1 roundtrips
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Select(c => new { c.Id, c.ParentCategoryId })
            .ToListAsync(cancellationToken);

        var currentParent = categories.FirstOrDefault(c => c.Id == potentialDescendantId)?.ParentCategoryId;

        var visited = new HashSet<Guid>();
        while (currentParent.HasValue)
        {
            if (currentParent.Value == ancestorId)
            {
                return true;
            }

            if (!visited.Add(currentParent.Value))
            {
                break; // Detected existing cycle, terminate traversal
            }

            currentParent = categories.FirstOrDefault(c => c.Id == currentParent.Value)?.ParentCategoryId;
        }

        return false;
    }

    private static CategoryResponseDto MapToDto(Category category)
    {
        return new CategoryResponseDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IconUrl = category.IconUrl,
            IsActive = category.IsActive,
            DisplayOrder = category.DisplayOrder,
            ParentCategoryId = category.ParentCategoryId,
            ParentCategoryName = category.ParentCategory?.Name,
            SubCategoriesCount = category.SubCategories?.Count ?? 0,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }

    private static CategoryTreeResponseDto BuildTreeItem(Category category, ILookup<Guid?, Category> lookup)
    {
        return new CategoryTreeResponseDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IconUrl = category.IconUrl,
            IsActive = category.IsActive,
            DisplayOrder = category.DisplayOrder,
            ParentCategoryId = category.ParentCategoryId,
            Children = lookup[category.Id].Select(child => BuildTreeItem(child, lookup)).ToList()
        };
    }
}
