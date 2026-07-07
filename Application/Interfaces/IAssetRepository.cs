using Application.DTOs;
using Domain.Entities;

namespace Application.Interfaces;

public interface IAssetRepository
{
    Task<List<Asset>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Asset?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Asset asset,
        CancellationToken cancellationToken = default);

    void Update(Asset asset);

    void Delete(Asset asset);
}
