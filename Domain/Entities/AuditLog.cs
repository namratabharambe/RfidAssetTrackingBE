using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class AuditLog
    {
        public Guid Id { get; set; }

        public string UserName { get; set; } = null!;

        public string Action { get; set; } = null!;

        public string EntityName { get; set; } = null!;

        public Guid EntityId { get; set; }

        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        public DateTime CreatedOn { get; set; }
    }
}
