using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Application.Auth.Commands.RefreshToken
{
    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResponseDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;

        public RefreshTokenCommandHandler(IUnitOfWork unitOfWork, IAuthService authService, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _configuration = configuration;
        }

        public async Task<LoginResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            var tokenRepo = _unitOfWork.Repository<Domain.Entities.RefreshToken>();
            var tokens = await tokenRepo.GetFilteredAsync(t => t.Token == request.RefreshTokenDto.RefreshToken, cancellationToken, t => t.User);
            var refreshToken = tokens.FirstOrDefault();

            if (refreshToken == null || !refreshToken.IsActive)
                throw new UnauthorizedAccessException("Invalid refresh token.");

            var userRepo = _unitOfWork.Repository<User>();
            var user = await userRepo.GetByIdAsync(refreshToken.UserId, cancellationToken, u => u.UserRoles, u => u.Site);
            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("User is not active.");

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

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["Secret"] ?? "EnterpriseRFIDAssetTrackingGPSERPSecretKeySecretKey";
            var issuer = jwtSettings["Issuer"] ?? "TrackItAPI";
            var audience = jwtSettings["Audience"] ?? "TrackItClient";
            var expiresMinutes = Convert.ToInt32(jwtSettings["ExpiresMinutes"] ?? "525600");

            var token = _authService.GenerateJwtToken(user, secretKey, issuer, audience, expiresMinutes);

            var newRefreshToken = await _authService.GenerateRefreshTokenAsync(user.Id, request.RemoteIpAddress, cancellationToken);

            refreshToken.Revoked = DateTime.UtcNow;
            refreshToken.RevokedByIp = request.RemoteIpAddress;
            refreshToken.ReplacedByToken = newRefreshToken.Token;

            tokenRepo.Update(refreshToken);
            await tokenRepo.AddAsync(newRefreshToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var rolesList = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            var permissionsList = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code)).Distinct().ToList();

            var userDto = new UserDto(user.Id, user.Username, user.Email, user.IsActive, user.SiteId, user.Site?.Name, rolesList, permissionsList);

            return new LoginResponseDto(token, newRefreshToken.Token, userDto);
        }
    }
}
