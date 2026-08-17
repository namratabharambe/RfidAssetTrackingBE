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
                var isGlobalSuperAdmin = HttpContext.User.IsInRole("Super Admin") 
                                      || HttpContext.User.HasClaim(c => c.Type == "allowed_site_ids" && c.Value == "ALL")
                                      || HttpContext.User.HasClaim(c => c.Type == "sites" && c.Value == "GLOBAL_ALL_SITES");

                if (!targetSiteId.HasValue && !isGlobalSuperAdmin)
                {
                    var siteClaim = HttpContext.User.FindFirst("siteId")?.Value ?? HttpContext.User.FindFirst("site_id")?.Value;
                    if (Guid.TryParse(siteClaim, out var g)) targetSiteId = g;
                }

                if (!targetWhId.HasValue && !isGlobalSuperAdmin)
                {
                    var whClaim = HttpContext.User.FindFirst("warehouseId")?.Value ?? HttpContext.User.FindFirst("warehouse_id")?.Value;
                    if (Guid.TryParse(whClaim, out var g)) targetWhId = g;
                }
            }

            var data = await _mediator.Send(new GetDashboardDataQuery(targetSiteId, targetWhId), cancellationToken);
            return Ok(data);
        }
    }
}
