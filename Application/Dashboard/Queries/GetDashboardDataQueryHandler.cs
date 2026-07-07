using Application.DTOs;
using Application.Interfaces;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Dashboard.Queries
{
    public class GetDashboardDataQueryHandler : IRequestHandler<GetDashboardDataQuery, DashboardDto>
    {
        private readonly IDashboardService _dashboardService;

        public GetDashboardDataQueryHandler(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<DashboardDto> Handle(GetDashboardDataQuery request, CancellationToken cancellationToken)
        {
            return await _dashboardService.GetDashboardDataAsync(cancellationToken);
        }
    }
}
