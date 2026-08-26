using Application.DTOs;
using Application.Auth.Commands.Login;
using Application.Auth.Commands.RefreshToken;
using Application.Auth.Commands.Logout;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;

        public AuthController(IMediator mediator, IUnitOfWork unitOfWork, IAuthService authService, IConfiguration configuration)
        {
            _mediator = mediator;
            _unitOfWork = unitOfWork;
            _authService = authService;
            _configuration = configuration;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto loginDto, CancellationToken cancellationToken)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var result = await _mediator.Send(new LoginCommand(loginDto, ipAddress), cancellationToken);
                return Ok(result);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Database or Server Login Error: {ex.Message}", detail = ex.InnerException?.Message });
            }
        }

        [HttpPost("/api/admin/users/login")]
        [AllowAnonymous]
        public async Task<ActionResult> HandheldLogin([FromBody] CompatibilityLoginRequest request, CancellationToken cancellationToken)
        {
            // Android sends both 'username' and 'email' fields — use whichever is populated
            var identity = request.Username ?? request.Email ?? "";
            try
            {
                var loginDto = new LoginDto(identity, request.Password);
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var result = await _mediator.Send(new LoginCommand(loginDto, ipAddress), cancellationToken);

                var roleId = result.User.Roles.FirstOrDefault() ?? "operator";

                return Ok(new
                {
                    message = "Login successful",
                    token = result.Token,
                    user = new
                    {
                        userId = result.User.Id.ToString(),
                        userName = result.User.Username,
                        email = result.User.Email,
                        siteId = result.User.SiteId?.ToString() ?? Guid.Empty.ToString(),
                        roleId = roleId,
                        roleName = roleId,
                        clientType = "AssetTracking"
                    }
                });
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Login Error: {ex.Message}", detail = ex.InnerException?.Message });
            }
        }

        public class CompatibilityLoginRequest
        {
            public string? Username { get; set; }  // Android sends 'username'
            public string? Email { get; set; }     // Also accept 'email'
            public string Password { get; set; } = null!;
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponseDto>> Refresh(RefreshTokenDto tokenDto, CancellationToken cancellationToken)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var result = await _mediator.Send(new RefreshTokenCommand(tokenDto, ipAddress), cancellationToken);
                return Ok(result);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenDto tokenDto, CancellationToken cancellationToken)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _mediator.Send(new LogoutCommand(tokenDto, ipAddress), cancellationToken);
            return Ok(new { Message = "Logged out successfully." });
        }

        [HttpPost("switch-context")]
        [Authorize]
        public async Task<ActionResult<LoginResponseDto>> SwitchContext([FromBody] SwitchContextDto request, CancellationToken cancellationToken)
        {
            try
            {
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdStr, out var userId))
                {
                    return Unauthorized(new { message = "Invalid user claims token." });
                }

                var userRepo = _unitOfWork.Repository<User>();
                var users = await userRepo.GetFilteredAsync(u => u.Id == userId, cancellationToken, u => u.UserRoles, u => u.Site);
                var user = users.FirstOrDefault();
                if (user == null || !user.IsActive)
                {
                    return NotFound(new { message = "User not found or inactive." });
                }

                foreach (var ur in user.UserRoles)
                {
                    var role = await _unitOfWork.Repository<Role>().GetByIdAsync(ur.RoleId, cancellationToken, r => r.RolePermissions);
                    if (role != null)
                    {
                        ur.Role = role;
                        foreach (var rp in role.RolePermissions)
                        {
                            var permission = await _unitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId, cancellationToken);
                            if (permission != null) rp.Permission = permission;
                        }
                    }
                }

                var allSites = await _unitOfWork.Repository<Site>().GetAllAsync(cancellationToken);
                var allWarehouses = await _unitOfWork.Repository<Warehouse>().GetAllAsync(cancellationToken);

                Guid? targetWhId = request.WarehouseId;
                Guid? targetSiteId = request.SiteId;

                if (targetWhId.HasValue)
                {
                    targetSiteId = null;
                }
                if (!targetSiteId.HasValue && !targetWhId.HasValue)
                {
                    targetSiteId = user.SiteId;
                }

                user.SiteId = targetSiteId;
                user.Site = null;

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
                var isSuperAdmin = user.UserRoles.Any(ur => ur.Role != null && (ur.Role.Name == "Super Admin" || ur.Role.Name == "System Administrator"));

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

                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["Secret"] ?? "EnterpriseRFIDAssetTrackingGPSERPSecretKeySecretKey";
                var issuer = jwtSettings["Issuer"] ?? "TrackItAPI";
                var audience = jwtSettings["Audience"] ?? "TrackItClient";
                var expiresMinutes = Convert.ToInt32(jwtSettings["ExpiresMinutes"] ?? "525600");

                List<Site> siteContextList;
                List<Warehouse> warehouseContextList;

                if (targetWhId.HasValue)
                {
                    // Warehouse selected: return ONLY warehouse data, NO site data
                    warehouseContextList = userAllowedWarehouses.Where(w => w.Id == targetWhId.Value).ToList();
                    siteContextList = new List<Site>();
                }
                else if (targetSiteId.HasValue)
                {
                    // Site selected: return ONLY site data, NO warehouse data
                    siteContextList = userAllowedSites.Where(s => s.Id == targetSiteId.Value).ToList();
                    warehouseContextList = new List<Warehouse>();
                }
                else
                {
                    // Global context: return all assigned sites and warehouses
                    siteContextList = userAllowedSites;
                    warehouseContextList = userAllowedWarehouses;
                }

                user.SiteId = targetWhId.HasValue ? null : targetSiteId;
                user.Site = null;

                var targetRole = request.Role;
                var newToken = _authService.GenerateJwtToken(user, secretKey, issuer, audience, expiresMinutes, siteContextList, warehouseContextList, targetWhId, targetRole);
                var refreshToken = await _authService.GenerateRefreshTokenAsync(user.Id, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", cancellationToken);

                var rolesList = user.UserRoles.Select(ur => ur.Role?.Name ?? "User").Distinct().ToList();
                if (!string.IsNullOrWhiteSpace(targetRole) && !rolesList.Contains(targetRole))
                {
                    rolesList.Insert(0, targetRole);
                }
                var permissionsList = user.UserRoles.SelectMany(ur => ur.Role?.RolePermissions?.Select(rp => rp.Permission?.Code ?? "") ?? new List<string>()).Distinct().Where(p => !string.IsNullOrEmpty(p)).ToList();

                var fullAllowedSiteDtos = userAllowedSites.Select(s => new SiteDto(s.Id, s.Code, s.Name, s.Address)).ToList();
                var fullAllowedWarehouseDtos = userAllowedWarehouses.Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Address)).ToList();

                string? activeContextName = null;
                Guid? activeSiteGuid = null;

                if (targetWhId.HasValue)
                {
                    var wh = userAllowedWarehouses.FirstOrDefault(w => w.Id == targetWhId.Value);
                    activeContextName = wh?.Name ?? "Warehouse";
                    activeSiteGuid = null;
                }
                else if (targetSiteId.HasValue)
                {
                    var st = userAllowedSites.FirstOrDefault(s => s.Id == targetSiteId.Value);
                    activeContextName = st?.Name ?? "Site";
                    activeSiteGuid = st?.Id;
                }
                else
                {
                    activeContextName = "All Contexts";
                    activeSiteGuid = null;
                }

                var userDto = new UserDto(user.Id, user.Username, user.Email, user.IsActive, activeSiteGuid, activeContextName, rolesList, permissionsList, fullAllowedSiteDtos, fullAllowedWarehouseDtos);

                return Ok(new LoginResponseDto(newToken, refreshToken.Token, userDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error switching context: {ex.Message}" });
            }
        }
    }
}
