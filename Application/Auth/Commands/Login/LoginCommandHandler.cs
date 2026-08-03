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

            var token = _authService.GenerateJwtToken(user, secretKey, issuer, audience, expiresMinutes);
            
            var refreshToken = await _authService.GenerateRefreshTokenAsync(user.Id, request.RemoteIpAddress, cancellationToken);

            await _unitOfWork.Repository<Domain.Entities.RefreshToken>().AddAsync(refreshToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var rolesList = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            var permissionsList = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code)).Distinct().ToList();

            var userDto = new UserDto(user.Id, user.Username, user.Email, user.IsActive, user.SiteId, user.Site?.Name, rolesList, permissionsList);

            return new LoginResponseDto(token, refreshToken.Token, userDto);
        }
    }
}
