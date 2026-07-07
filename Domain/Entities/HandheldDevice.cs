using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class HandheldDevice : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string DeviceSerial { get; set; } = null!;
        public string? Model { get; set; }
        public DeviceStatus Status { get; set; } = DeviceStatus.Online;
        public Guid? AssignedUserId { get; set; }
        public User? AssignedUser { get; set; }
        public ICollection<ScanSession> ScanSessions { get; set; } = new List<ScanSession>();
    }
}
