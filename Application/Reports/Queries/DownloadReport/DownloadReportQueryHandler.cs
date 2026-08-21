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
                "assets" => await _reportService.GenerateAssetReportAsync("csv", request.StartDate, request.EndDate, request.SiteId, request.SiteName, cancellationToken),
                "movements" => await _reportService.GenerateMovementReportAsync("csv", request.StartDate, request.EndDate, request.SiteId, request.SiteName, cancellationToken),
                "inventory" => await _reportService.GenerateInventoryReportAsync("csv", request.StartDate, request.EndDate, request.SiteId, request.SiteName, cancellationToken),
                "rfid" => await _reportService.GenerateRFIDReportAsync("csv", request.StartDate, request.EndDate, request.SiteId, request.SiteName, cancellationToken),
                "gps" => await _reportService.GenerateGPSReportAsync("csv", request.StartDate, request.EndDate, request.SiteId, request.SiteName, cancellationToken),
                "audits" => await _reportService.GenerateAuditReportAsync("csv", request.StartDate, request.EndDate, request.SiteId, request.SiteName, cancellationToken),
                "users" => await _reportService.GenerateUserReportAsync("csv", request.StartDate, request.EndDate, request.SiteId, request.SiteName, cancellationToken),
                "transfers" or "transfer" => await _reportService.GenerateTransferReportAsync("csv", request.StartDate, request.EndDate, request.SiteId, request.SiteName, cancellationToken),
                "issuances" or "issuance" or "issues" or "issue" => await _reportService.GenerateIssuanceReportAsync("csv", request.StartDate, request.EndDate, request.SiteId, request.SiteName, cancellationToken),
                _ => throw new ArgumentException("Invalid report type")
            };
        }
    }
}
