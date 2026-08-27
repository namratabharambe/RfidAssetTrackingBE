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
            Guid? effectiveSiteId = request.SiteId;
            Guid? effectiveWhId = request.WarehouseId;

            var allActiveUsers = await _unitOfWork.Repository<User>().GetFilteredAsync(
                u => !u.IsDeleted,
                cancellationToken
            );

            List<User> users;

            if (request.IsSuperAdmin)
            {
                users = allActiveUsers.ToList();
            }
            else
            {
                var allowedSiteGuids = new HashSet<Guid>(request.AllowedSiteIds ?? new List<Guid>());
                if (effectiveSiteId.HasValue) allowedSiteGuids.Add(effectiveSiteId.Value);
                var allowedSiteStrings = allowedSiteGuids.Select(id => id.ToString().ToLower()).ToHashSet();

                var allowedWhGuids = new HashSet<Guid>(request.AllowedWarehouseIds ?? new List<Guid>());
                if (effectiveWhId.HasValue) allowedWhGuids.Add(effectiveWhId.Value);
                var allowedWhStrings = allowedWhGuids.Select(id => id.ToString().ToLower()).ToHashSet();

                users = allActiveUsers.Where(u =>
                {
                    var uEmail = (u.Email ?? "").Trim().ToLower();
                    var uName = (u.Username ?? "").Trim().ToLower();
                    var uCreatedBy = (u.CreatedBy ?? "").Trim().ToLower();

                    // 1. Current user themselves or users created by them
                    if (uEmail == identityLower || uName == identityLower || (!string.IsNullOrEmpty(identityLower) && (uCreatedBy == identityLower || uCreatedBy.Contains(identityLower))))
                    {
                        return true;
                    }

                    // 2. Check primary site match
                    if (u.SiteId.HasValue && allowedSiteGuids.Contains(u.SiteId.Value))
                    {
                        return true;
                    }

                    // 3. Check allowed sites CSV match
                    if (!string.IsNullOrWhiteSpace(u.AllowedSiteIds))
                    {
                        var parts = u.AllowedSiteIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Any(p => allowedSiteStrings.Contains(p.Trim().ToLower())))
                        {
                            return true;
                        }
                    }

                    // 4. Check allowed warehouses CSV match
                    if (!string.IsNullOrWhiteSpace(u.AllowedWarehouseIds))
                    {
                        var parts = u.AllowedWarehouseIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Any(p => allowedWhStrings.Contains(p.Trim().ToLower())))
                        {
                            return true;
                        }
                    }

                    return false;
                }).ToList();
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

            var allSites = (await _unitOfWork.Repository<Site>().GetFilteredAsync(s => !s.IsDeleted, cancellationToken)).ToList();
            var allWhs = (await _unitOfWork.Repository<Warehouse>().GetFilteredAsync(w => !w.IsDeleted, cancellationToken)).ToList();

            var userDtos = _mapper.Map<List<UserDto>>(users);

            for (int i = 0; i < users.Count; i++)
            {
                var user = users[i];
                var dto = userDtos[i];

                // 1. Resolve all assigned site IDs (AllowedSiteIds string + SiteId)
                var siteIdList = new List<Guid>();
                if (!string.IsNullOrWhiteSpace(user.AllowedSiteIds))
                {
                    foreach (var part in user.AllowedSiteIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Guid.TryParse(part.Trim(), out var g) && !siteIdList.Contains(g))
                            siteIdList.Add(g);
                    }
                }
                if (user.SiteId.HasValue && !siteIdList.Contains(user.SiteId.Value))
                {
                    siteIdList.Add(user.SiteId.Value);
                }

                // 2. Map full SiteDto objects with Name and Code
                var matchedSites = allSites.Where(s => siteIdList.Contains(s.Id)).Select(s => _mapper.Map<SiteDto>(s)).ToList();
                dto.AllowedSites = matchedSites;
                dto.SelectedSiteIds = siteIdList;

                if (string.IsNullOrWhiteSpace(dto.SiteName))
                {
                    var primarySiteName = allSites.FirstOrDefault(s => s.Id == user.SiteId)?.Name ?? matchedSites.FirstOrDefault()?.Name;
                    if (!string.IsNullOrWhiteSpace(primarySiteName)) dto.SiteName = primarySiteName;
                }

                // 3. Resolve all assigned warehouse IDs (AllowedWarehouseIds string)
                var whIdList = new List<Guid>();
                if (!string.IsNullOrWhiteSpace(user.AllowedWarehouseIds))
                {
                    foreach (var part in user.AllowedWarehouseIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Guid.TryParse(part.Trim(), out var g) && !whIdList.Contains(g))
                            whIdList.Add(g);
                    }
                }
                var matchedWhs = allWhs.Where(w => whIdList.Contains(w.Id)).Select(w => _mapper.Map<WarehouseDto>(w)).ToList();
                dto.AllowedWarehouses = matchedWhs;
                dto.SelectedWarehouseIds = whIdList;
            }

            return userDtos;
        }
    }
}
