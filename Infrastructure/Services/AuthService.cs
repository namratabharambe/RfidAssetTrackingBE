using Application.Interfaces;
using Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        public string HashPassword(string password, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);
            using var rfc2898 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
            var hashBytes = rfc2898.GetBytes(32);
            return Convert.ToBase64String(hashBytes);
        }

        public string GenerateSalt()
        {
            var bytes = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        public string GenerateJwtToken(User user, string secretKey, string issuer, string audience, int expiresMinutes, IEnumerable<Site>? allowedSites = null, IEnumerable<Warehouse>? allowedWarehouses = null)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(secretKey);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email)
            };

            // 1. Site Access Claims
            if (user.SiteId.HasValue)
            {
                claims.Add(new Claim("siteId", user.SiteId.Value.ToString()));
                claims.Add(new Claim("sites", user.SiteId.Value.ToString()));
            }
            else
            {
                var sitesList = allowedSites?.ToList() ?? new List<Site>();
                if (user.Site != null && !sitesList.Any(s => s.Id == user.Site.Id))
                {
                    sitesList.Add(user.Site);
                }
                foreach (var site in sitesList)
                {
                    claims.Add(new Claim("sites", site.Id.ToString()));
                }
            }

            // 2. Role Access Claims
            var roleNames = new List<string>();
            if (user.UserRoles != null && user.UserRoles.Any())
            {
                foreach (var userRole in user.UserRoles)
                {
                    if (userRole.Role != null && !string.IsNullOrEmpty(userRole.Role.Name))
                    {
                        roleNames.Add(userRole.Role.Name);
                        claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
                    }
                }
            }

            if (!roleNames.Any())
            {
                var defaultRole = user.SiteId.HasValue ? "Site Admin" : "Super Admin";
                claims.Add(new Claim(ClaimTypes.Role, defaultRole));
                claims.Add(new Claim("roles", defaultRole));
            }
            else
            {
                claims.Add(new Claim("roles", string.Join(",", roleNames)));
            }

            // 3. Warehouse Access Claims
            var warehousesList = allowedWarehouses?.ToList() ?? new List<Warehouse>();
            if (user.SiteId.HasValue)
            {
                warehousesList = warehousesList.Where(w => w.SiteId == user.SiteId.Value).ToList();
            }
            if (warehousesList.Any())
            {
                foreach (var wh in warehousesList)
                {
                    claims.Add(new Claim("warehouses", wh.Id.ToString()));
                }
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expiresMinutes),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, string ipAddress, CancellationToken cancellationToken = default)
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = Convert.ToBase64String(bytes),
                Expires = DateTime.UtcNow.AddYears(1),
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            return Task.FromResult(refreshToken);
        }
    }
}
