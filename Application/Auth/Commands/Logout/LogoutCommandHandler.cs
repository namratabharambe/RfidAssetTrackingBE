using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Auth.Commands.Logout
{
    public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
    {
        private readonly IUnitOfWork _unitOfWork;

        public LogoutCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            var tokenRepo = _unitOfWork.Repository<Domain.Entities.RefreshToken>();
            var tokens = await tokenRepo.GetFilteredAsync(t => t.Token == request.RefreshTokenDto.RefreshToken, cancellationToken);
            var refreshToken = tokens.FirstOrDefault();

            if (refreshToken != null)
            {
                refreshToken.Revoked = DateTime.UtcNow;
                refreshToken.RevokedByIp = request.RemoteIpAddress;
                tokenRepo.Update(refreshToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
