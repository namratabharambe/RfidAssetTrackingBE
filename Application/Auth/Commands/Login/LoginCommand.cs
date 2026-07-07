using Application.DTOs;
using MediatR;

namespace Application.Auth.Commands.Login
{
    public record LoginCommand(LoginDto LoginDto, string RemoteIpAddress) : IRequest<LoginResponseDto>;
}
