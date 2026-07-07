using Domain.Common;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class User : BaseEntity
    {
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string PasswordSalt { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        /// <summary>Null = SuperAdmin (all sites). Set = restricted to this site.</summary>
        public Guid? SiteId { get; set; }
        public Site? Site { get; set; }
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}

