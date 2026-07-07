using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Driver : BaseEntity
    {
        public string FullName { get; set; } = null!;

        public string? PhoneNumber { get; set; }

        public string? Email { get; set; }
    }
}
