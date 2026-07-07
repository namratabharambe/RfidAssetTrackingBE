using Application.DTOs;
using MediatR;

namespace Application.Auth.Commands.RefreshToken
{
    public record RefreshTokenCommand(RefreshTokenDto RefreshTokenDto, string RemoteIpAddress) : IRequest<LoginResponseDto>;
}
