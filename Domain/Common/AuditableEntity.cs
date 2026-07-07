using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Common
{
    public abstract class AuditableEntity : BaseEntity
    {
        public DateTime CreatedOn { get; set; }

        public string? CreatedBy { get; set; }

        public DateTime? UpdatedOn { get; set; }

        public string? UpdatedBy { get; set; }
    }
}
