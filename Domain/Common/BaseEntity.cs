using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Common
{

    public abstract class BaseEntity
    {
        public Guid Id { get; set; }

        public DateTime CreatedOn { get; set; }

        public string? CreatedBy { get; set; }

        public DateTime? UpdatedOn { get; set; }

        public string? UpdatedBy { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedOn { get; set; }

        public string? DeletedBy { get; set; }

        public uint RowVersion { get; set; }
    }
}
