using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AssetRepository : IAssetRepository
    {
        private readonly AssetTrackingDbContext _context;

        public AssetRepository(AssetTrackingDbContext context)
        {
            _context = context;
        }

        public async Task<List<Asset>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.Assets
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<Asset?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Assets
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task AddAsync(
            Asset asset,
            CancellationToken cancellationToken = default)
        {
            await _context.Assets.AddAsync(asset, cancellationToken);
        }

        public void Update(Asset asset)
        {
            _context.Assets.Update(asset);
        }

        public void Delete(Asset asset)
        {
            asset.IsDeleted = true;
            asset.DeletedOn = DateTime.UtcNow;
        }
    }
}
