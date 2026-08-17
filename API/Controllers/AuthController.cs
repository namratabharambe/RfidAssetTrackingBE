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
        public async Task<ActionResult<LoginResponseDto>> Login(LoginDto loginDto, CancellationToken cancellationToken)
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

                Guid? targetSiteId = request.SiteId.HasValue ? request.SiteId.Value : user.SiteId;
                Guid? targetWhId = request.WarehouseId;

                user.SiteId = targetSiteId;

                List<Site> userAllowedSites;
                List<Warehouse> userAllowedWarehouses;

                var identityLower = (user.Username + " " + user.Email).ToLower();

                if (identityLower.Contains("devam"))
                {
                    userAllowedSites = allSites.Where(s => s.Name.ToLower().Contains("devam") || s.Code.ToLower().Contains("devam")).ToList();
                    var siteIds = userAllowedSites.Select(s => s.Id).ToHashSet();
                    userAllowedWarehouses = allWarehouses.Where(w => siteIds.Contains(w.SiteId) || w.Name.ToLower().Contains("devam") || w.Code.ToLower().Contains("devam")).ToList();
                }
                else
                {
                    userAllowedSites = allSites.ToList();
                    userAllowedWarehouses = allWarehouses.ToList();
                }

                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["Secret"] ?? "EnterpriseRFIDAssetTrackingGPSERPSecretKeySecretKey";
                var issuer = jwtSettings["Issuer"] ?? "TrackItAPI";
                var audience = jwtSettings["Audience"] ?? "TrackItClient";
                var expiresMinutes = Convert.ToInt32(jwtSettings["ExpiresMinutes"] ?? "525600");

                var newToken = _authService.GenerateJwtToken(user, secretKey, issuer, audience, expiresMinutes, userAllowedSites, userAllowedWarehouses);
                var refreshToken = await _authService.GenerateRefreshTokenAsync(user.Id, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", cancellationToken);

                var rolesList = user.UserRoles.Select(ur => ur.Role?.Name ?? "User").Distinct().ToList();
                var permissionsList = user.UserRoles.SelectMany(ur => ur.Role?.RolePermissions?.Select(rp => rp.Permission?.Code ?? "") ?? new List<string>()).Distinct().Where(p => !string.IsNullOrEmpty(p)).ToList();

                var allowedSiteDtos = userAllowedSites.Select(s => new SiteDto(s.Id, s.Code, s.Name, s.Address)).ToList();
                var allowedWarehouseDtos = userAllowedWarehouses.Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Address, w.SiteId, userAllowedSites.FirstOrDefault(s => s.Id == w.SiteId)?.Name ?? "")).ToList();

                var userDto = new UserDto(user.Id, user.Username, user.Email, user.IsActive, user.SiteId, user.Site?.Name, rolesList, permissionsList, allowedSiteDtos, allowedWarehouseDtos);

                return Ok(new LoginResponseDto(newToken, refreshToken.Token, userDto));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error switching context: {ex.Message}" });
            }
        }
    }
}
