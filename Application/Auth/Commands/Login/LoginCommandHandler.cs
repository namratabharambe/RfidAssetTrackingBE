using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Application.Auth.Commands.Login
{
    public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponseDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;

        public LoginCommandHandler(IUnitOfWork unitOfWork, IAuthService authService, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _configuration = configuration;
        }

        public async Task<LoginResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var identity = (request.LoginDto.Username ?? request.LoginDto.Email ?? "").Trim().ToLower();
            if (string.IsNullOrEmpty(identity))
                throw new UnauthorizedAccessException("Invalid username or password.");

            var userRepo = _unitOfWork.Repository<User>();
            var users = await userRepo.GetFilteredAsync(
                u => u.Username.ToLower() == identity || u.Email.ToLower() == identity, 
                cancellationToken, 
                u => u.UserRoles,
                u => u.Site);

            var user = users.FirstOrDefault();
            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Invalid username or password.");

            foreach (var ur in user.UserRoles)
            {
                var role = await _unitOfWork.Repository<Role>().GetByIdAsync(ur.RoleId, cancellationToken, r => r.RolePermissions);
                if (role != null)
                {
                    ur.Role = role;
                    foreach (var rp in role.RolePermissions)
                    {
                        var permission = await _unitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId, cancellationToken);
                        if (permission != null)
                        {
                            rp.Permission = permission;
                        }
                    }
                }
            }

            var expectedHash = _authService.HashPassword(request.LoginDto.Password, user.PasswordSalt);
            if (user.PasswordHash != expectedHash)
                throw new UnauthorizedAccessException("Invalid username or password.");

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["Secret"] ?? "EnterpriseRFIDAssetTrackingGPSERPSecretKeySecretKey";
            var issuer = jwtSettings["Issuer"] ?? "TrackItAPI";
            var audience = jwtSettings["Audience"] ?? "TrackItClient";
            var expiresMinutes = Convert.ToInt32(jwtSettings["ExpiresMinutes"] ?? "525600");

            // Fetch all allowed sites and warehouses for this user context
            var allSites = await _unitOfWork.Repository<Site>().GetAllAsync(cancellationToken);
            var allWarehouses = await _unitOfWork.Repository<Warehouse>().GetAllAsync(cancellationToken);

            // Parse explicitly assigned Site IDs from user
            var assignedSiteIds = new HashSet<Guid>();
            if (!string.IsNullOrWhiteSpace(user.AllowedSiteIds))
            {
                foreach (var idStr in user.AllowedSiteIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Guid.TryParse(idStr.Trim(), out var g)) assignedSiteIds.Add(g);
                }
            }
            if (user.SiteId.HasValue) assignedSiteIds.Add(user.SiteId.Value);

            // Parse explicitly assigned Warehouse IDs from user
            var assignedWhIds = new HashSet<Guid>();
            if (!string.IsNullOrWhiteSpace(user.AllowedWarehouseIds))
            {
                foreach (var idStr in user.AllowedWarehouseIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Guid.TryParse(idStr.Trim(), out var g)) assignedWhIds.Add(g);
                }
            }

            var identityLower = (user.Username + " " + user.Email).ToLower();
            var isSuperAdmin = user.UserRoles.Any(ur => ur.Role.Name == "Super Admin" || ur.Role.Name == "System Administrator");

            List<Site> userAllowedSites;
            List<Warehouse> userAllowedWarehouses;

            if (isSuperAdmin && !assignedSiteIds.Any() && !assignedWhIds.Any())
            {
                userAllowedSites = allSites.ToList();
                userAllowedWarehouses = allWarehouses.ToList();
            }
            else if (assignedSiteIds.Any() || assignedWhIds.Any())
            {
                userAllowedSites = allSites.Where(s => assignedSiteIds.Contains(s.Id)).ToList();
                if (!userAllowedSites.Any() && user.SiteId.HasValue)
                {
                    var s = allSites.FirstOrDefault(x => x.Id == user.SiteId.Value);
                    if (s != null) userAllowedSites.Add(s);
                }

                if (assignedWhIds.Any())
                {
                    userAllowedWarehouses = allWarehouses.Where(w => assignedWhIds.Contains(w.Id)).ToList();
                }
                else if (isSuperAdmin)
                {
                    userAllowedWarehouses = allWarehouses.ToList();
                }
                else
                {
                    userAllowedWarehouses = new List<Warehouse>();
                }
            }
            else
            {
                if (user.SiteId.HasValue)
                {
                    userAllowedSites = allSites.Where(s => s.Id == user.SiteId.Value).ToList();
                    userAllowedWarehouses = isSuperAdmin ? allWarehouses.ToList() : new List<Warehouse>();
                }
                else
                {
                    userAllowedSites = isSuperAdmin ? allSites.ToList() : new List<Site>();
                    userAllowedWarehouses = isSuperAdmin ? allWarehouses.ToList() : new List<Warehouse>();
                }
            }

            if (!user.SiteId.HasValue && userAllowedSites.Any())
            {
                user.SiteId = userAllowedSites.First().Id;
            }

            var token = _authService.GenerateJwtToken(user, secretKey, issuer, audience, expiresMinutes, userAllowedSites, userAllowedWarehouses);
            
            var refreshToken = await _authService.GenerateRefreshTokenAsync(user.Id, request.RemoteIpAddress, cancellationToken);

            await _unitOfWork.Repository<Domain.Entities.RefreshToken>().AddAsync(refreshToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var rolesList = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            var permissionsList = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code)).Distinct().ToList();

            var allowedSiteDtos = userAllowedSites.Select(s => new SiteDto(s.Id, s.Code, s.Name, s.Address)).ToList();
            var allowedWarehouseDtos = userAllowedWarehouses.Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Address)).ToList();

            var activeSiteName = userAllowedSites.FirstOrDefault(s => s.Id == user.SiteId)?.Name ?? user.Site?.Name;
            var userDto = new UserDto(user.Id, user.Username, user.Email, user.IsActive, user.SiteId, activeSiteName, rolesList, permissionsList, allowedSiteDtos, allowedWarehouseDtos);

            return new LoginResponseDto(token, refreshToken.Token, userDto);
        }
    }
}
