using System.Security.Claims;
using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Category;
using DawwerOS.Business.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

/// <summary>
/// Administrator endpoints for managing the master category taxonomy.
/// </summary>
[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = "Admin")]
public class AdminCategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public AdminCategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Gets all categories with optional filtering by active status and search keyword.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryResponseDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryResponseDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetCategoriesAsync(isActive, search, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the hierarchical category tree.
    /// </summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryTreeResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryTreeResponseDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryTreeResponseDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategoryTree(
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetCategoryTreeAsync(isActive, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a single category by its unique identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetCategoryByIdAsync(id, activeOnly: null, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Creates a new master category or subcategory.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<CategoryResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _categoryService.CreateCategoryAsync(request, adminId.Value, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetCategoryById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Updates an existing master category.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<CategoryResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _categoryService.UpdateCategoryAsync(id, request, adminId.Value, cancellationToken);
        if (!result.Success)
        {
            if (result.Message == "Category not found." || result.Message.Contains("not found"))
            {
                return NotFound(result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Deletes a master category if it has no subcategories.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Unauthorized access."));
        }

        var result = await _categoryService.DeleteCategoryAsync(id, adminId.Value, cancellationToken);
        if (!result.Success)
        {
            if (result.Message == "Category not found.")
            {
                return NotFound(result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Activates a category making it available to stores and public users.
    /// </summary>
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateCategory(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<CategoryResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _categoryService.ActivateCategoryAsync(id, adminId.Value, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Deactivates a category hiding it from public and store listings.
    /// </summary>
    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateCategory(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
        {
            return Unauthorized(ApiResponse<CategoryResponseDto>.Fail("Unauthorized access."));
        }

        var result = await _categoryService.DeactivateCategoryAsync(id, adminId.Value, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
