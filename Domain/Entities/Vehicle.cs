using Domain.Common;
using System;

namespace Domain.Entities
{
    public class Vehicle : BaseEntity
    {
        public string DeviceNum { get; set; } = null!;
        public string RegName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public double Lat { get; set; }
        public double Lon { get; set; }
        public double Speed { get; set; }
        public double Direction { get; set; }
        public double Battery { get; set; }
        public DateTime GpsTime { get; set; }
        public DateTime UpdateTime { get; set; }

        public Guid VehicleID => Id;
    }
}
