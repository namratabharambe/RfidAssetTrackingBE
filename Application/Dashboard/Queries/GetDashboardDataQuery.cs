using Application.DTOs;
using MediatR;

namespace Application.Dashboard.Queries
{
    public record GetDashboardDataQuery : IRequest<DashboardDto>;
}
