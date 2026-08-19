using Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IAuthService
    {
        string HashPassword(string password, string salt);
        string GenerateSalt();
        string GenerateJwtToken(User user, string secretKey, string issuer, string audience, int expiresMinutes, System.Collections.Generic.IEnumerable<Site>? allowedSites = null, System.Collections.Generic.IEnumerable<Warehouse>? allowedWarehouses = null, Guid? activeWarehouseId = null);
        Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, string ipAddress, CancellationToken cancellationToken = default);
    }
}
