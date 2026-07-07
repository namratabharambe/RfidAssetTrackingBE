using Domain.Common;
using Domain.Enums;
using System;

namespace Domain.Entities
{
    public class ScanEvent : BaseEntity
    {
        public Guid ScanSessionId { get; set; }
        public ScanSession ScanSession { get; set; } = null!;
        public string EpcCode { get; set; } = null!;
        public string? TidCode { get; set; }
        public DateTime Timestamp { get; set; }
        public int Rssi { get; set; }
        public int AntennaIndex { get; set; }
        public Guid? ReaderId { get; set; }
        public Reader? Reader { get; set; }
        public Guid? HandheldDeviceId { get; set; }
        public HandheldDevice? HandheldDevice { get; set; }
        public ScanStatus Status { get; set; } = ScanStatus.Matched;
    }
}
