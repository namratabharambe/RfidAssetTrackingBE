using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Common
{
    public abstract class SoftDeletableEntity : AuditableEntity
    {
        public bool IsDeleted { get; set; }

        public DateTime? DeletedOn { get; set; }

        public string? DeletedBy { get; set; }

        public uint RowVersion { get; set; }
    }
}
