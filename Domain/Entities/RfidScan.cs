using Domain.Common;
using System;

namespace Domain.Entities
{
    public class RfidScan : BaseEntity
    {
        public Guid ScanId
        {
            get => Id;
            set => Id = value;
        }
        public string Epc { get; set; } = null!;
        public double Rssi { get; set; }
        public string ReaderId { get; set; } = null!;
        public string SiteId { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public string type { get; set; } = null!;
        public DateTime? ProcessedAt { get; set; }
    }
}
