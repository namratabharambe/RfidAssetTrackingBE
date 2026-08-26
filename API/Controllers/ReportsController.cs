using Application.Reports.Queries.DownloadReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ReportsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{reportType}")]
        public async Task<IActionResult> DownloadReport(
            string reportType,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] string? siteName = null,
            [FromQuery] string? site = null,
            CancellationToken cancellationToken = default)
        {
            var start = startDate ?? fromDate;
            var end = endDate ?? toDate;
            var targetSiteName = siteName ?? site;

            Guid? targetSiteId = siteId;
            if (!targetSiteId.HasValue && Request.Headers.TryGetValue("X-Site-Id", out var hSite) && Guid.TryParse(hSite.FirstOrDefault(), out var parsedHSite) && parsedHSite != Guid.Empty)
            {
                targetSiteId = parsedHSite;
            }

            if (!targetSiteId.HasValue && string.IsNullOrEmpty(targetSiteName) && HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var siteClaim = HttpContext.User.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);
                if (Guid.TryParse(siteClaim, out var g)) targetSiteId = g;
            }

            try
            {
                var bytes = await _mediator.Send(new DownloadReportQuery(reportType, start, end, targetSiteId, targetSiteName), cancellationToken);
                return File(bytes, "text/csv", $"{reportType}Report.csv");
            }
            catch (System.ArgumentException)
            {
                return BadRequest("Invalid report type");
            }
        }
    }
}
