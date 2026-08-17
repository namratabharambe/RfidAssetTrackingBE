using Application.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardDto> GetDashboardDataAsync(Guid? siteId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default);
    }
}
