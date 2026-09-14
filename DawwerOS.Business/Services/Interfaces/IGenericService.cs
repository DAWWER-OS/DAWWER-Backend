using System.Linq.Expressions;
using DawwerOS.Business.Common;
using DawwerOS.DAL.Entities;

namespace DawwerOS.Business.Services.Interfaces;

/// <summary>
/// A comprehensive generic service contract supporting separate Response, Create, and Update DTOs.
/// </summary>
/// <typeparam name="TEntity">The database entity deriving from BaseEntity.</typeparam>
/// <typeparam name="TResponseDto">The DTO returned to callers.</typeparam>
/// <typeparam name="TCreateDto">The payload used when creating a new entity.</typeparam>
/// <typeparam name="TUpdateDto">The payload used when updating an existing entity.</typeparam>
public interface IGenericService<TEntity, TResponseDto, in TCreateDto, in TUpdateDto>
    where TEntity : BaseEntity
{
    Task<ApiResponse<IEnumerable<TResponseDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ApiResponse<IEnumerable<TResponseDto>>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiResponse<TResponseDto>> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<int>> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TResponseDto>> CreateAsync(
        TCreateDto dto,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TResponseDto>> UpdateAsync(
        Guid id,
        TUpdateDto dto,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic service contract for entities where Response, Create, and Update share the same DTO.
/// </summary>
public interface IGenericService<TEntity, TDto> : IGenericService<TEntity, TDto, TDto, TDto>
    where TEntity : BaseEntity
{
}
