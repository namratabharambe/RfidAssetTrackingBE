using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class GPSDevice : BaseEntity
    {
        public string Imei { get; set; } = null!;
        public string? SimNumber { get; set; }
        public int BatteryLevel { get; set; } = 100;
        public DeviceStatus Status { get; set; } = DeviceStatus.Online;
        public Guid? AssetId { get; set; }
        public Asset? Asset { get; set; }
        public ICollection<GPSHistory> GPSHistories { get; set; } = new List<GPSHistory>();
    }
}
