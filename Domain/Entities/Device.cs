using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Device : BaseEntity
    {
        public string DeviceNumber { get; set; } = null!;

        public string Name { get; set; } = null!;

        public DeviceType DeviceType { get; set; }

        public DeviceStatus Status { get; set; }
    }
}
