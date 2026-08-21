using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IReportService
    {
        Task<byte[]> GenerateAssetReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateMovementReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateInventoryReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateRFIDReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateGPSReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateAuditReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateUserReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateTransferReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateIssuanceReportAsync(string format, DateTime? startDate = null, DateTime? endDate = null, Guid? siteId = null, string? siteName = null, CancellationToken cancellationToken = default);
    }
}
