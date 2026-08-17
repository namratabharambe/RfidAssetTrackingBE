using Application.DTOs;
using MediatR;

namespace Application.Dashboard.Queries
{
    public record GetDashboardDataQuery(Guid? SiteId = null, Guid? WarehouseId = null) : IRequest<DashboardDto>;
}
