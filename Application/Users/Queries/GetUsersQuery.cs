using Application.DTOs;
using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Users.Queries
{
    public record GetUsersQuery(Guid? SiteId = null, Guid? WarehouseId = null) : IRequest<IEnumerable<UserDto>>;
}
