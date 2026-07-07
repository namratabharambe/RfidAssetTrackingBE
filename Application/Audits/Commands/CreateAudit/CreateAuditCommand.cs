using MediatR;
using System;

namespace Application.Audits.Commands.CreateAudit
{
    public sealed record CreateAuditCommand(
        string Title,
        Guid AuditorUserId,
        Guid? LocationId
    ) : IRequest<Guid>;
}
