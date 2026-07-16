using System;
using System.Collections.Generic;

namespace AssetTracking.Rfid.Domain.Entities
{
    public class RfidTag
    {
        public Guid RfidTagId { get; set; }
        public string TagName { get; set; } = null!;
    }

    public class Truck
    {
        public Guid TruckId { get; set; }
        public string TruckNumber { get; set; } = null!;
        public Guid? DriverId { get; set; }
        public Guid SiteId { get; set; }
        
        public Guid? RfidTagId { get; set; }
        public RfidTag? RfidTag { get; set; }
    }

    public class Equipment
    {
        public Guid EquipmentId { get; set; }
        public Guid SiteId { get; set; }
        public DateTime? LastDateTimeOut { get; set; }
        public DateTime? LastDateTimeIn { get; set; }
        
        public Guid? RfidTagId { get; set; }
        public RfidTag? RfidTag { get; set; }
    }

    public class GateEvent
    {
        public Guid GateEventId { get; set; }
        public Guid? TruckId { get; set; }
        public Guid? DriverId { get; set; }
        public Guid ReaderId { get; set; }
        public Guid SiteId { get; set; }
        public DateTime EventTime { get; set; }
        public string EventType { get; set; } = null!;
        public string Status { get; set; } = null!;
        
        public ICollection<GateEventItem> Items { get; set; } = new List<GateEventItem>();
    }

    public class GateEventItem
    {
        public Guid GateEventItemId { get; set; }
        public Guid GateEventId { get; set; }
        public Guid EquipmentId { get; set; }
        public string Epc { get; set; } = null!;
        public DateTime EventTime { get; set; }
        public Guid SiteId { get; set; }
        public string Type { get; set; } = null!;
    }

    public class ActiveTruckSession
    {
        public Guid Id { get; set; }
        public Guid ReaderId { get; set; }
        public Guid SiteId { get; set; }
        public Guid? TruckId { get; set; }
        public Guid? DriverId { get; set; }
        public Guid? GateEventId { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class TruckEquipmentAssignment
    {
        public Guid AssignmentId { get; set; }
        public Guid? TruckId { get; set; }
        public Guid? DriverId { get; set; }
        public Guid EquipmentId { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public Guid SiteId { get; set; }
        public string Status { get; set; } = null!;
        public string Type { get; set; } = null!;
    }

    public class MissingEquipmentCase
    {
        public Guid MissingEquipmentCaseId { get; set; }
        public Guid? TruckId { get; set; }
        public Guid? DriverId { get; set; }
        public Guid SiteId { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int StatusId { get; set; }
        public int SeverityId { get; set; }
        
        public ICollection<MissingEquipmentCaseItem> Items { get; set; } = new List<MissingEquipmentCaseItem>();
    }

    public class MissingEquipmentCaseItem
    {
        public Guid MissingEquipmentCaseItemId { get; set; }
        public Guid MissingEquipmentCaseId { get; set; }
        public MissingEquipmentCase? MissingEquipmentCase { get; set; }
        public Guid EquipmentId { get; set; }
        public string Epc { get; set; } = null!;
        public int StatusId { get; set; }
        public Guid SiteId { get; set; }
        public bool IsRecovered { get; set; }
        public DateTime? RecoveredAt { get; set; }
    }

    public class MissingEquipmentStatus
    {
        public int StatusId { get; set; }
        public string Code { get; set; } = null!;
        public string Description { get; set; } = null!;
        public bool IsFinal { get; set; }
    }

    public class MissingEquipmentSeverity
    {
        public int SeverityId { get; set; }
        public int Priority { get; set; }
        public string Description { get; set; } = null!;
        public decimal CostThreshold { get; set; }
    }

    public class Alert
    {
        public Guid AlertId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Severity { get; set; } = null!;
        public string Source { get; set; } = null!;
        public Guid SiteId { get; set; }
        public string Message { get; set; } = null!;
    }
}
