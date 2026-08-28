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

        public string GenerateJwtToken(User user, string secretKey, string issuer, string audience, int expiresMinutes, IEnumerable<Site>? allowedSites = null, IEnumerable<Warehouse>? allowedWarehouses = null, Guid? activeWarehouseId = null, string? activeRole = null)
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
            var sitesList = allowedSites?.ToList() ?? new List<Site>();
            if (user.SiteId.HasValue && !sitesList.Any(s => s.Id == user.SiteId.Value))
            {
                if (user.Site != null) sitesList.Add(user.Site);
            }

            if (user.SiteId.HasValue)
            {
                claims.Add(new Claim("siteId", user.SiteId.Value.ToString()));
                claims.Add(new Claim("site_id", user.SiteId.Value.ToString()));
            }

            foreach (var site in sitesList)
            {
                claims.Add(new Claim("sites", site.Id.ToString()));
                claims.Add(new Claim("allowed_site_ids", site.Id.ToString()));
            }

            if (sitesList.Any())
            {
                var siteCsv = string.Join(",", sitesList.Select(s => s.Id));
                claims.Add(new Claim("allowed_site_ids_csv", siteCsv));
                claims.Add(new Claim("sites_csv", siteCsv));
                try
                {
                    claims.Add(new Claim("sites_json", System.Text.Json.JsonSerializer.Serialize(sitesList.Select(s => new { Id = s.Id, Name = s.Name, Code = s.Code }))));
                }
                catch { }
            }

            // 2. Role Access Claims
            var roleNames = new List<string>();
            if (user.UserRoles != null && user.UserRoles.Any())
            {
                foreach (var userRole in user.UserRoles)
                {
                    var rName = userRole.Role?.Name;
                    if (string.IsNullOrEmpty(rName))
                    {
                        if (userRole.RoleId == Guid.Parse("e1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62")) rName = "Super Admin";
                        else if (userRole.RoleId == Guid.Parse("e2a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62")) rName = "Site Admin";
                        else if (userRole.RoleId == Guid.Parse("0e9d0e01-c0a0-438d-a823-0544dc67ad6f")) rName = "Project Manager";
                        else if (userRole.RoleId == Guid.Parse("e68f87f4-8b80-4d37-b787-a660dc0f8a56")) rName = "Store Keeper";
                        else if (userRole.RoleId == Guid.Parse("a5736683-b651-4b38-aa67-5a07baa4d156")) rName = "Safety";
                        else if (userRole.RoleId == Guid.Parse("e3a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62")) rName = "Supervisor";
                        else if (userRole.RoleId == Guid.Parse("e4a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62")) rName = "Driver";
                        else if (userRole.RoleId == Guid.Parse("e5a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62")) rName = "Viewer";
                    }

                    if (!string.IsNullOrEmpty(rName) && !roleNames.Contains(rName))
                    {
                        roleNames.Add(rName);
                        claims.Add(new Claim(ClaimTypes.Role, rName));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(activeRole))
            {
                claims.Add(new Claim("role", activeRole));
                claims.Add(new Claim("active_role", activeRole));
                if (!roleNames.Contains(activeRole))
                {
                    claims.Add(new Claim(ClaimTypes.Role, activeRole));
                    roleNames.Add(activeRole);
                }
            }

            if (!roleNames.Any())
            {
                var defaultRole = user.SiteId.HasValue ? "Site Admin" : "Super Admin";
                claims.Add(new Claim(ClaimTypes.Role, defaultRole));
                claims.Add(new Claim("roles", defaultRole));
                claims.Add(new Claim("role", defaultRole));
            }
            else
            {
                claims.Add(new Claim("roles", string.Join(",", roleNames)));
                if (string.IsNullOrWhiteSpace(activeRole))
                {
                    claims.Add(new Claim("role", roleNames.First()));
                }
            }

            // 3. Warehouse Access Claims
            var warehousesList = allowedWarehouses?.ToList() ?? new List<Warehouse>();
            if (activeWarehouseId.HasValue)
            {
                claims.Add(new Claim("warehouseId", activeWarehouseId.Value.ToString()));
            }

            foreach (var wh in warehousesList)
            {
                claims.Add(new Claim("warehouses", wh.Id.ToString()));
                claims.Add(new Claim("allowed_warehouse_ids", wh.Id.ToString()));
            }

            if (warehousesList.Any())
            {
                var whCsv = string.Join(",", warehousesList.Select(w => w.Id));
                claims.Add(new Claim("allowed_warehouse_ids_csv", whCsv));
                claims.Add(new Claim("warehouses_csv", whCsv));
                    claims.Add(new Claim("warehouses_json", System.Text.Json.JsonSerializer.Serialize(warehousesList.Select(w => new { Id = w.Id, Name = w.Name, Code = w.Code }))));
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
