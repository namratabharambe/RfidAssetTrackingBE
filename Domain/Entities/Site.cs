using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Site : BaseEntity
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Address { get; set; }
        /// <summary>Warehouse | Manufacturing | DistributionCenter | Hub</summary>
        public string? SiteType { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
