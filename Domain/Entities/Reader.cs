using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Reader : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string IpAddress { get; set; } = null!;
        public int Port { get; set; } = 5084;
        public DeviceStatus Status { get; set; } = DeviceStatus.Online;
        public int AntennaCount { get; set; } = 4;
        public int PowerDbm { get; set; } = 30;
        public string? Model { get; set; }
        public Guid SiteId { get; set; }
        public Site Site { get; set; } = null!;
        public ICollection<ScanSession> ScanSessions { get; set; } = new List<ScanSession>();
    }
}
