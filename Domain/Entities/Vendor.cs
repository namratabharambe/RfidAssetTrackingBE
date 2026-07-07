using Domain.Common;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Vendor : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string? ContactName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}
