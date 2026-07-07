using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Alert : BaseEntity
    {
        public Guid? AssetId { get; set; }

        public Asset? Asset { get; set; }

        public AlertType AlertType { get; set; }

        public AlertSeverity Severity { get; set; }

        public string Title { get; set; } = null!;

        public string Message { get; set; } = null!;

        public bool IsResolved { get; set; }

        public DateTime? ResolvedDate { get; set; }

        public Guid? ResolvedByUserId { get; set; }

        public User? ResolvedByUser { get; set; }
    }
}
