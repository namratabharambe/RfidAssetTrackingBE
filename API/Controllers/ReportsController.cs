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
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] string? start = null,
            [FromQuery] string? end = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] string? siteName = null,
            [FromQuery] string? site = null,
            CancellationToken cancellationToken = default)
        {
            var rawStart = !string.IsNullOrWhiteSpace(startDate) ? startDate :
                           !string.IsNullOrWhiteSpace(fromDate) ? fromDate :
                           !string.IsNullOrWhiteSpace(from) ? from : start;

            var rawEnd = !string.IsNullOrWhiteSpace(endDate) ? endDate :
                         !string.IsNullOrWhiteSpace(toDate) ? toDate :
                         !string.IsNullOrWhiteSpace(to) ? to : end;

            DateTime? parsedStart = null;
            if (!string.IsNullOrWhiteSpace(rawStart) && DateTime.TryParse(rawStart, out var sVal))
            {
                parsedStart = sVal;
            }

            DateTime? parsedEnd = null;
            if (!string.IsNullOrWhiteSpace(rawEnd) && DateTime.TryParse(rawEnd, out var eVal))
            {
                parsedEnd = eVal;
            }

            var targetSiteName = (siteName ?? site)?.Trim();
            if (targetSiteName != null && (targetSiteName.Equals("All Sites", StringComparison.OrdinalIgnoreCase) || targetSiteName.Equals("All", StringComparison.OrdinalIgnoreCase)))
            {
                targetSiteName = null;
            }

            Guid? targetSiteId = siteId;
            if (!targetSiteId.HasValue && Request.Headers.TryGetValue("X-Site-Id", out var hSite) && Guid.TryParse(hSite.FirstOrDefault(), out var parsedHSite) && parsedHSite != Guid.Empty)
            {
                targetSiteId = parsedHSite;
            }

            try
            {
                var bytes = await _mediator.Send(new DownloadReportQuery(reportType, parsedStart, parsedEnd, targetSiteId, targetSiteName), cancellationToken);
                return File(bytes, "text/csv", $"{reportType}Report.csv");
            }
            catch (System.ArgumentException)
            {
                return BadRequest("Invalid report type");
            }
        }
    }
}
