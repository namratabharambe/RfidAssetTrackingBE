using Application.DTOs;
using Application.Auth.Commands.Login;
using Application.Auth.Commands.RefreshToken;
using Application.Auth.Commands.Logout;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
