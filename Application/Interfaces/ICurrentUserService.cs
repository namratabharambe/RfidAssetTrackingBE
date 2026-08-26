using System;
using System.Collections.Generic;

namespace Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        string? Username { get; }
        string? Email { get; }
        Guid? SiteId { get; }
        Guid? WarehouseId { get; }
        string? ActiveRole { get; }
        List<string> Roles { get; }
        List<Guid> AllowedSiteIds { get; }
        List<Guid> AllowedWarehouseIds { get; }
        bool IsSuperAdmin { get; }
        bool IsAuthenticated { get; }
    }
}
