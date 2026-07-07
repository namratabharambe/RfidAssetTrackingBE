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
        public async Task<IActionResult> DownloadReport(string reportType, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _mediator.Send(new DownloadReportQuery(reportType), cancellationToken);
                return File(bytes, "text/csv", $"{reportType}Report.csv");
            }
            catch (System.ArgumentException)
            {
                return BadRequest("Invalid report type");
            }
        }
    }
}
