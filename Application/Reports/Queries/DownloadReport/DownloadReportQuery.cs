using MediatR;
using System;

namespace Application.Reports.Queries.DownloadReport
{
    public record DownloadReportQuery(string ReportType, DateTime? StartDate = null, DateTime? EndDate = null, Guid? SiteId = null, string? SiteName = null) : IRequest<byte[]>;
}
