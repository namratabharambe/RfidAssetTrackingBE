using Domain.Common;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Manufacturer : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string? ContactInfo { get; set; }
        public string? SupportEmail { get; set; }
        public string? SupportPhone { get; set; }
        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}
