using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Category;
using DawwerOS.Business.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DawwerOS.WebApi.Controllers;

/// <summary>
/// Public and merchant endpoints for viewing active master categories and taxonomy.
/// </summary>
[ApiController]
[Route("api/categories")]
[AllowAnonymous]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Gets all active categories with optional keyword search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetCategoriesAsync(activeOnly: true, search: search, cancellationToken: cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the hierarchical active category tree.
    /// </summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryTreeResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategoryTree(CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetCategoryTreeAsync(activeOnly: true, cancellationToken: cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets an active category by its unique identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetCategoryByIdAsync(id, activeOnly: true, cancellationToken: cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
