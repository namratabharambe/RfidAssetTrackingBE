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

namespace API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
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
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("/api/admin/users/login")]
        [AllowAnonymous]
        public async Task<ActionResult> HandheldLogin([FromBody] CompatibilityLoginRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var loginDto = new LoginDto(request.Email, request.Password);
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
        }

        public class CompatibilityLoginRequest
        {
            public string Email { get; set; } = null!;
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
    }
}
