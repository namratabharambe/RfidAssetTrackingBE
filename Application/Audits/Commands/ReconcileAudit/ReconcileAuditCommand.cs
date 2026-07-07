using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Audits.Commands.ReconcileAudit
{
    public sealed record ReconcileAuditCommand(
        Guid AuditId,
        List<string> ScannedEpcs,
        Guid? ScannedLocationId
    ) : IRequest<bool>;
}
