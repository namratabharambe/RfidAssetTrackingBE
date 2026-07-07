using Application.DTOs;
using MediatR;
using System.Collections.Generic;

namespace Application.Users.Queries
{
    public record GetUsersQuery : IRequest<IEnumerable<UserDto>>;
}
