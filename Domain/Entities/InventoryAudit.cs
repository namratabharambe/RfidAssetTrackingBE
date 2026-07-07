using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class InventoryAudit : BaseEntity
    {
        public string Title { get; set; } = null!;
        public DateTime AuditDate { get; set; }
        public AuditStatus Status { get; set; } = AuditStatus.Scheduled;
        public Guid AuditorUserId { get; set; }
        public User AuditorUser { get; set; } = null!;
        public ICollection<InventoryAuditItem> AuditItems { get; set; } = new List<InventoryAuditItem>();
    }
}
