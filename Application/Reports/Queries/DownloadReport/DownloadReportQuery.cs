using MediatR;

namespace Application.Reports.Queries.DownloadReport
{
    public record DownloadReportQuery(string ReportType) : IRequest<byte[]>;
}
