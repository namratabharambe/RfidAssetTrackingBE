using Application.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DashboardController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData([FromQuery] Guid? siteId = null, [FromQuery] Guid? warehouseId = null, CancellationToken cancellationToken = default)
        {
            Guid? targetSiteId = siteId;
            Guid? targetWhId = warehouseId;

            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                if (!targetSiteId.HasValue)
                {
                    var siteClaim = HttpContext.User.Claims
                        .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                        .Select(c => c.Value)
                        .FirstOrDefault(v => Guid.TryParse(v, out _));
                    if (Guid.TryParse(siteClaim, out var g)) targetSiteId = g;
                }

                if (!targetWhId.HasValue)
                {
                    var whClaim = HttpContext.User.Claims
                        .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                        .Select(c => c.Value)
                        .FirstOrDefault(v => Guid.TryParse(v, out _));
                    if (Guid.TryParse(whClaim, out var g)) targetWhId = g;
                }
            }

            var data = await _mediator.Send(new GetDashboardDataQuery(targetSiteId, targetWhId), cancellationToken);
            return Ok(data);
        }
    }
}
