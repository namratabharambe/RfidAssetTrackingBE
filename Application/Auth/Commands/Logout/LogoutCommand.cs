using Application.DTOs;
using MediatR;

namespace Application.Auth.Commands.Logout
{
    public record LogoutCommand(RefreshTokenDto RefreshTokenDto, string RemoteIpAddress) : IRequest;
}
