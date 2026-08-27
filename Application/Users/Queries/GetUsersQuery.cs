using Application.DTOs;
using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Users.Queries
{
    public record GetUsersQuery(
        Guid? SiteId = null, 
        Guid? WarehouseId = null, 
        List<Guid>? AllowedSiteIds = null, 
        List<Guid>? AllowedWarehouseIds = null, 
        string? CurrentUserIdentity = null, 
        bool IsSuperAdmin = false
    ) : IRequest<IEnumerable<UserDto>>;
}
