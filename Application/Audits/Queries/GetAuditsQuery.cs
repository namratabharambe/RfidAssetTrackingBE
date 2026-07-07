using Application.DTOs;
using MediatR;
using System.Collections.Generic;

namespace Application.Audits.Queries
{
    public record GetAuditsQuery : IRequest<IEnumerable<InventoryAuditDto>>;
}
