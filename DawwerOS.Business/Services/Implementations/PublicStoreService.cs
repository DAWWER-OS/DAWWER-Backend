using DawwerOS.Business.Common;
using DawwerOS.Business.DTOs.Store;
using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using DawwerOS.DAL.Repositories.Interfaces;

namespace DawwerOS.Business.Services.Implementations;

public class PublicStoreService : IPublicStoreService
{
    private readonly IGenericRepository<Store> _storeRepository;

    public PublicStoreService(IGenericRepository<Store> storeRepository)
    {
        _storeRepository = storeRepository;
    }

    public async Task<ApiResponse<IEnumerable<PublicStoreResponseDto>>> GetActiveApprovedStoresAsync(
        string? city = null,
        string? search = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Public filter rule: Strictly return ONLY Approved and Active stores
        var stores = await _storeRepository.FindAsync(
            s => s.VerificationStatus == StoreVerificationStatus.Approved && s.Status == StoreStatus.Active,
            cancellationToken);

        var query = stores.AsQueryable();

        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = city.Trim().ToLowerInvariant();
            query = query.Where(s => s.City != null && s.City.ToLower().Contains(normalizedCity));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.Name.ToLower().Contains(normalizedSearch) ||
                (s.Description != null && s.Description.ToLower().Contains(normalizedSearch)));
        }

        var results = query
            .OrderBy(s => s.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new PublicStoreResponseDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                PhoneNumber = s.PhoneNumber,
                Email = s.Email,
                Address = s.Address,
                City = s.City,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                LogoUrl = s.LogoUrl,
                CoverImageUrl = s.CoverImageUrl,
                Status = s.Status.ToString()
            })
            .ToList();

        return ApiResponse<IEnumerable<PublicStoreResponseDto>>.Ok(results);
    }

    public async Task<ApiResponse<PublicStoreResponseDto>> GetStoreByIdAsync(
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        var store = await _storeRepository.GetByIdAsync(storeId, cancellationToken);

        // Security rule: Unapproved or Suspended stores MUST NEVER be returned publicly!
        if (store == null ||
            store.VerificationStatus != StoreVerificationStatus.Approved ||
            store.Status != StoreStatus.Active)
        {
            return ApiResponse<PublicStoreResponseDto>.Fail("Store not found or unavailable.");
        }

        var dto = new PublicStoreResponseDto
        {
            Id = store.Id,
            Name = store.Name,
            Description = store.Description,
            PhoneNumber = store.PhoneNumber,
            Email = store.Email,
            Address = store.Address,
            City = store.City,
            Latitude = store.Latitude,
            Longitude = store.Longitude,
            LogoUrl = store.LogoUrl,
            CoverImageUrl = store.CoverImageUrl,
            Status = store.Status.ToString()
        };

        return ApiResponse<PublicStoreResponseDto>.Ok(dto);
    }
}
