using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class ScanSession : BaseEntity
    {
        public string SessionName { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public Guid? ReaderId { get; set; }
        public Reader? Reader { get; set; }
        public Guid? HandheldDeviceId { get; set; }
        public HandheldDevice? HandheldDevice { get; set; }
        public bool IsRunning { get; set; } = true;
        public ICollection<ScanEvent> ScanEvents { get; set; } = new List<ScanEvent>();
    }
}
