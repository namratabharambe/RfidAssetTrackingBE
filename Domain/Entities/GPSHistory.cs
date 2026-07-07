using Domain.Common;
using System;

namespace Domain.Entities
{
    public class GPSHistory : BaseEntity
    {
        public Guid GPSDeviceId { get; set; }
        public GPSDevice GPSDevice { get; set; } = null!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        public double Heading { get; set; }
        public DateTime Timestamp { get; set; }
        public string? GeofenceStatus { get; set; }
    }
}
