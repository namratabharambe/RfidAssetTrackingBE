using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IReportService
    {
        Task<byte[]> GenerateAssetReportAsync(string format, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateMovementReportAsync(string format, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateInventoryReportAsync(string format, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateRFIDReportAsync(string format, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateGPSReportAsync(string format, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateAuditReportAsync(string format, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateUserReportAsync(string format, CancellationToken cancellationToken = default);
    }
}
