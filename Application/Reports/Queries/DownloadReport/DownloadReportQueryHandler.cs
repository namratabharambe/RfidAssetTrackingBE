using Application.Interfaces;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Reports.Queries.DownloadReport
{
    public class DownloadReportQueryHandler : IRequestHandler<DownloadReportQuery, byte[]>
    {
        private readonly IReportService _reportService;

        public DownloadReportQueryHandler(IReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<byte[]> Handle(DownloadReportQuery request, CancellationToken cancellationToken)
        {
            return request.ReportType.ToLower() switch
            {
                "assets" => await _reportService.GenerateAssetReportAsync("csv", cancellationToken),
                "movements" => await _reportService.GenerateMovementReportAsync("csv", cancellationToken),
                "inventory" => await _reportService.GenerateInventoryReportAsync("csv", cancellationToken),
                "rfid" => await _reportService.GenerateRFIDReportAsync("csv", cancellationToken),
                "gps" => await _reportService.GenerateGPSReportAsync("csv", cancellationToken),
                "audits" => await _reportService.GenerateAuditReportAsync("csv", cancellationToken),
                "users" => await _reportService.GenerateUserReportAsync("csv", cancellationToken),
                _ => throw new ArgumentException("Invalid report type")
            };
        }
    }
}
