using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;

namespace Application.Users.Queries
{
    public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, IEnumerable<UserDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetUsersQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        {
            var identityLower = (request.CurrentUserIdentity ?? "").Trim().ToLower();
            bool isDevamContext = identityLower.Contains("devam");

            Guid? effectiveSiteId = request.SiteId;

            if (!effectiveSiteId.HasValue && request.WarehouseId.HasValue)
            {
                var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(request.WarehouseId.Value, cancellationToken);
                if (warehouse != null)
                {
                    effectiveSiteId = warehouse.SiteId;
                }
            }

            List<User> users;

            if (isDevamContext)
            {
                // Isolate Devam tenant: return only users associated with Devam
                var devamSites = await _unitOfWork.Repository<Site>().GetFilteredAsync(
                    s => !s.IsDeleted && (s.Name.ToLower().Contains("devam") || s.Code.ToLower().Contains("devam")),
                    cancellationToken
                );
                var devamSiteIds = devamSites.Select(s => (Guid?)s.Id).ToList();

                users = await _unitOfWork.Repository<User>().GetFilteredAsync(
                    u => !u.IsDeleted && (
                        (u.SiteId.HasValue && devamSiteIds.Contains(u.SiteId)) ||
                        u.Username.ToLower().Contains("devam") ||
                        u.Email.ToLower().Contains("devam")
                    ),
                    cancellationToken
                );
            }
            else if (request.AllowedSiteIds != null && request.AllowedSiteIds.Any() && !request.IsSuperAdmin)
            {
                var allowedIds = request.AllowedSiteIds.Select(id => (Guid?)id).ToList();
                var allowedIdStrings = request.AllowedSiteIds.Select(id => id.ToString().ToLower()).ToList();

                var allActiveUsers = await _unitOfWork.Repository<User>().GetFilteredAsync(
                    u => !u.IsDeleted,
                    cancellationToken
                );

                users = allActiveUsers.Where(u => 
                    (u.SiteId.HasValue && allowedIds.Contains(u.SiteId)) ||
                    (!string.IsNullOrEmpty(u.AllowedSiteIds) && allowedIdStrings.Any(aid => u.AllowedSiteIds.ToLower().Contains(aid))) ||
                    u.Username.Equals(identityLower, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Equals(identityLower, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
            else if (effectiveSiteId.HasValue && !request.IsSuperAdmin)
            {
                users = await _unitOfWork.Repository<User>().GetFilteredAsync(
                    u => !u.IsDeleted && u.SiteId == effectiveSiteId.Value,
                    cancellationToken
                );
            }
            else
            {
                users = await _unitOfWork.Repository<User>().GetFilteredAsync(
                    u => !u.IsDeleted,
                    cancellationToken
                );
            }
            
            // Populate roles and permissions
            foreach (var user in users)
            {
                var userWithRoles = await _unitOfWork.Repository<User>().GetByIdAsync(user.Id, cancellationToken, u => u.UserRoles, u => u.Site);
                if (userWithRoles != null)
                {
                    user.UserRoles = userWithRoles.UserRoles;
                    user.Site = userWithRoles.Site;
                    user.SiteId = userWithRoles.SiteId;
                    foreach (var ur in user.UserRoles)
                    {
                        var role = await _unitOfWork.Repository<Role>().GetByIdAsync(ur.RoleId, cancellationToken, r => r.RolePermissions);
                        if (role != null)
                        {
                            ur.Role = role;
                            foreach (var rp in role.RolePermissions)
                            {
                                var permission = await _unitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId, cancellationToken);
                                if (permission != null) rp.Permission = permission;
                             }
                        }
                    }
                }
            }

            return _mapper.Map<List<UserDto>>(users);
        }
    }
}
