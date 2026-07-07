using Domain.Common;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Permission : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
