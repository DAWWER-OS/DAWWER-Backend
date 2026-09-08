using System.Linq.Expressions;
using DawwerOS.Business.Common;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Repositories.Interfaces;

namespace DawwerOS.Business.Services.Implementations;

/// <summary>
/// A comprehensive abstract generic service providing full CRUD, querying, lifecycle hooks, and manual DTO mapping.
/// </summary>
public abstract class GenericService<TEntity, TResponseDto, TCreateDto, TUpdateDto>
    : IGenericService<TEntity, TResponseDto, TCreateDto, TUpdateDto>
    where TEntity : BaseEntity
{
    protected readonly IGenericRepository<TEntity> _repository;

    protected GenericService(IGenericRepository<TEntity> repository)
    {
        _repository = repository;
    }

    protected virtual string EntityName => typeof(TEntity).Name;

    #region Abstract Mapping Methods

    /// <summary>
    /// Maps a database entity to its response DTO representation.
    /// </summary>
    protected abstract TResponseDto MapToResponseDto(TEntity entity);

    /// <summary>
    /// Maps a creation DTO to a new database entity.
    /// </summary>
    protected abstract TEntity MapToEntity(TCreateDto dto);

    /// <summary>
    /// Applies updates from an update DTO onto an existing database entity.
    /// </summary>
    protected abstract void UpdateEntity(TEntity entity, TUpdateDto dto);

    #endregion

    #region Lifecycle Hooks

    /// <summary>
    /// Executed before an entity is created. Return a failure response to abort creation with validation errors.
    /// </summary>
    protected virtual Task<ApiResponse<bool>?> BeforeCreateAsync(TCreateDto dto, CancellationToken cancellationToken)
        => Task.FromResult<ApiResponse<bool>?>(null);

    /// <summary>
    /// Executed after an entity is saved to the database.
    /// </summary>
    protected virtual Task AfterCreateAsync(TEntity entity, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Executed before an existing entity is updated. Return a failure response to abort update with validation errors.
    /// </summary>
    protected virtual Task<ApiResponse<bool>?> BeforeUpdateAsync(TEntity entity, TUpdateDto dto, CancellationToken cancellationToken)
        => Task.FromResult<ApiResponse<bool>?>(null);

    /// <summary>
    /// Executed after an updated entity is saved to the database.
    /// </summary>
    protected virtual Task AfterUpdateAsync(TEntity entity, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Executed before an entity is deleted. Return a failure response to abort deletion (e.g., foreign key restrictions).
    /// </summary>
    protected virtual Task<ApiResponse<bool>?> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken)
        => Task.FromResult<ApiResponse<bool>?>(null);

    /// <summary>
    /// Executed after an entity has been deleted from the database.
    /// </summary>
    protected virtual Task AfterDeleteAsync(TEntity entity, CancellationToken cancellationToken)
        => Task.CompletedTask;

    #endregion

    #region Query Operations

    public virtual async Task<ApiResponse<IEnumerable<TResponseDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        var dtos = entities.Select(MapToResponseDto);
        return ApiResponse<IEnumerable<TResponseDto>>.Ok(dtos, $"{EntityName} list retrieved successfully.");
    }

    public virtual async Task<ApiResponse<IEnumerable<TResponseDto>>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.FindAsync(predicate, cancellationToken);
        var dtos = entities.Select(MapToResponseDto);
        return ApiResponse<IEnumerable<TResponseDto>>.Ok(dtos, $"{EntityName} search results retrieved successfully.");
    }

    public virtual async Task<ApiResponse<TResponseDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return ApiResponse<TResponseDto>.Fail($"{EntityName} with ID {id} was not found.");
        }

        return ApiResponse<TResponseDto>.Ok(MapToResponseDto(entity), $"{EntityName} retrieved successfully.");
    }

    public virtual async Task<ApiResponse<TResponseDto>> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FirstOrDefaultAsync(predicate, cancellationToken);
        if (entity == null)
        {
            return ApiResponse<TResponseDto>.Fail($"{EntityName} matching the specified criteria was not found.");
        }

        return ApiResponse<TResponseDto>.Ok(MapToResponseDto(entity), $"{EntityName} retrieved successfully.");
    }

    public virtual async Task<ApiResponse<bool>> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        var exists = await _repository.AnyAsync(e => e.Id == id, cancellationToken);
        return ApiResponse<bool>.Ok(exists);
    }

    public virtual async Task<ApiResponse<bool>> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var exists = await _repository.AnyAsync(predicate, cancellationToken);
        return ApiResponse<bool>.Ok(exists);
    }

    public virtual async Task<ApiResponse<int>> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var count = await _repository.CountAsync(predicate, cancellationToken);
        return ApiResponse<int>.Ok(count);
    }

    #endregion

    #region Mutation Operations

    public virtual async Task<ApiResponse<TResponseDto>> CreateAsync(
        TCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var validation = await BeforeCreateAsync(dto, cancellationToken);
        if (validation != null && !validation.Success)
        {
            return ApiResponse<TResponseDto>.Fail(validation.Message, validation.Errors);
        }

        var entity = MapToEntity(dto);
        await _repository.AddAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AfterCreateAsync(entity, cancellationToken);

        return ApiResponse<TResponseDto>.Ok(MapToResponseDto(entity), $"{EntityName} created successfully.");
    }

    public virtual async Task<ApiResponse<TResponseDto>> UpdateAsync(
        int id,
        TUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return ApiResponse<TResponseDto>.Fail($"{EntityName} with ID {id} was not found.");
        }

        var validation = await BeforeUpdateAsync(entity, dto, cancellationToken);
        if (validation != null && !validation.Success)
        {
            return ApiResponse<TResponseDto>.Fail(validation.Message, validation.Errors);
        }

        UpdateEntity(entity, dto);
        _repository.Update(entity);
        await _repository.SaveChangesAsync(cancellationToken);

        await AfterUpdateAsync(entity, cancellationToken);

        return ApiResponse<TResponseDto>.Ok(MapToResponseDto(entity), $"{EntityName} updated successfully.");
    }

    public virtual async Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail($"{EntityName} with ID {id} was not found.");
        }

        var validation = await BeforeDeleteAsync(entity, cancellationToken);
        if (validation != null && !validation.Success)
        {
            return validation;
        }

        _repository.Delete(entity);
        await _repository.SaveChangesAsync(cancellationToken);

        await AfterDeleteAsync(entity, cancellationToken);

        return ApiResponse<bool>.Ok(true, $"{EntityName} deleted successfully.");
    }

    #endregion
}

/// <summary>
/// Convenient generic service base class for entities where Response, Create, and Update share the same DTO.
/// </summary>
public abstract class GenericService<TEntity, TDto>
    : GenericService<TEntity, TDto, TDto, TDto>, IGenericService<TEntity, TDto>
    where TEntity : BaseEntity
{
    protected GenericService(IGenericRepository<TEntity> repository)
        : base(repository)
    {
    }
}
