using Application.DTOs;
using Application.Users.Queries;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;

        public UsersController(IMediator mediator, IUnitOfWork unitOfWork, IMapper mapper, IAuthService authService)
        {
            _mediator = mediator;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _authService = authService;
        }

        private Guid? CurrentUserSiteId
        {
            get
            {
                var claim = User.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out _));
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        private Guid? CurrentUserWarehouseId
        {
            get
            {
                var claim = User.Claims
                    .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out _));
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAll(
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null)
        {
            var identity = User.Identity?.Name 
                ?? User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name || c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "unique_name" || c.Type == "email" || c.Type == "username")?.Value
                ?? "";

            var isSuperAdmin = User.IsInRole("Super Admin") || User.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Super Admin");

            var allowedSiteIds = User.Claims
                .Where(c => c.Type == "sites" || c.Type == "siteId" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .Distinct()
                .ToList();

            var targetSiteId = siteId ?? CurrentUserSiteId;
            var targetWarehouseId = warehouseId ?? CurrentUserWarehouseId;

            var users = await _mediator.Send(new GetUsersQuery(targetSiteId, targetWarehouseId, allowedSiteIds, identity, isSuperAdmin));
            return Ok(users);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(id, cancellationToken, u => u.UserRoles, u => u.Site);
            if (user == null) return NotFound();

            // Populate roles and permissions
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

            return Ok(_mapper.Map<UserDto>(user));
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto createDto, CancellationToken cancellationToken)
        {
            // Resolve role names to Role IDs if role names are provided
            var targetRoleIds = createDto.RoleIds != null ? new List<Guid>(createDto.RoleIds) : new List<Guid>();
            if (createDto.Roles != null && createDto.Roles.Any())
            {
                var allRoles = await _unitOfWork.Repository<Role>().GetAllAsync(cancellationToken);
                foreach (var roleName in createDto.Roles)
                {
                    var r = allRoles.FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
                    if (r != null && !targetRoleIds.Contains(r.Id)) targetRoleIds.Add(r.Id);
                }
            }
            if (!string.IsNullOrWhiteSpace(createDto.Role))
            {
                var allRoles = await _unitOfWork.Repository<Role>().GetAllAsync(cancellationToken);
                var r = allRoles.FirstOrDefault(x => x.Name.Equals(createDto.Role, StringComparison.OrdinalIgnoreCase));
                if (r != null && !targetRoleIds.Contains(r.Id)) targetRoleIds.Add(r.Id);
            }

            // Validate Site ID exists
            if (createDto.SiteId != null)
            {
                var site = await _unitOfWork.Repository<Site>().GetByIdAsync(createDto.SiteId.Value, cancellationToken);
                if (site == null)
                {
                    createDto = createDto with { SiteId = null };
                }
            }

            var salt = _authService.GenerateSalt();
            var hash = _authService.HashPassword(createDto.Password, salt);

            var allowedSitesStr = (createDto.AllowedSiteIds != null && createDto.AllowedSiteIds.Any())
                ? string.Join(",", createDto.AllowedSiteIds)
                : (createDto.SiteId.HasValue ? createDto.SiteId.Value.ToString() : null);

            var allowedWhsStr = (createDto.AllowedWarehouseIds != null && createDto.AllowedWarehouseIds.Any())
                ? string.Join(",", createDto.AllowedWarehouseIds)
                : null;

            var primarySiteId = createDto.SiteId ?? (createDto.AllowedSiteIds != null && createDto.AllowedSiteIds.Any() ? createDto.AllowedSiteIds.First() : null);

            var user = new User
            {
                Username = createDto.Username,
                Email = createDto.Email,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                SiteId = primarySiteId,
                AllowedSiteIds = allowedSitesStr,
                AllowedWarehouseIds = allowedWhsStr
            };

            await _unitOfWork.Repository<User>().AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (targetRoleIds.Any())
            {
                foreach (var roleId in targetRoleIds)
                {
                    var userRole = new UserRole { UserId = user.Id, RoleId = roleId };
                    await _unitOfWork.Repository<UserRole>().AddAsync(userRole, cancellationToken);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Fetch fully populated user to return
            var createdUser = await _unitOfWork.Repository<User>().GetByIdAsync(user.Id, cancellationToken, u => u.UserRoles, u => u.Site);
            if (createdUser != null)
            {
                foreach (var ur in createdUser.UserRoles)
                {
                    var r = await _unitOfWork.Repository<Role>().GetByIdAsync(ur.RoleId, cancellationToken, rp => rp.RolePermissions);
                    if (r != null)
                    {
                        ur.Role = r;
                        foreach (var rp in r.RolePermissions)
                        {
                            var p = await _unitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId, cancellationToken);
                            if (p != null) rp.Permission = p;
                        }
                    }
                }
                return CreatedAtAction(nameof(GetById), new { id = user.Id }, _mapper.Map<UserDto>(createdUser));
            }

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, _mapper.Map<UserDto>(user));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto updateDto, CancellationToken cancellationToken)
        {
            // Validate Role IDs exist
            if (updateDto.RoleIds != null && updateDto.RoleIds.Any())
            {
                foreach (var roleId in updateDto.RoleIds)
                {
                    var role = await _unitOfWork.Repository<Role>().GetByIdAsync(roleId, cancellationToken);
                    if (role == null)
                    {
                        return BadRequest($"Role with ID '{roleId}' does not exist.");
                    }
                }
            }

            // Validate Site ID exists
            if (updateDto.SiteId != null)
            {
                var site = await _unitOfWork.Repository<Site>().GetByIdAsync(updateDto.SiteId.Value, cancellationToken);
                if (site == null)
                {
                    return BadRequest($"Site with ID '{updateDto.SiteId}' does not exist.");
                }
            }

            var userRepo = _unitOfWork.Repository<User>();
            var user = await userRepo.GetByIdAsync(id, cancellationToken, u => u.UserRoles);
            if (user == null) return NotFound();

            var allowedSitesStr = (updateDto.AllowedSiteIds != null && updateDto.AllowedSiteIds.Any())
                ? string.Join(",", updateDto.AllowedSiteIds)
                : (updateDto.SiteId.HasValue ? updateDto.SiteId.Value.ToString() : user.AllowedSiteIds);

            var allowedWhsStr = (updateDto.AllowedWarehouseIds != null && updateDto.AllowedWarehouseIds.Any())
                ? string.Join(",", updateDto.AllowedWarehouseIds)
                : user.AllowedWarehouseIds;

            var primarySiteId = updateDto.SiteId ?? (updateDto.AllowedSiteIds != null && updateDto.AllowedSiteIds.Any() ? updateDto.AllowedSiteIds.First() : user.SiteId);

            user.Username = updateDto.Username;
            user.Email = updateDto.Email;
            user.IsActive = updateDto.IsActive;
            user.SiteId = primarySiteId;
            user.AllowedSiteIds = allowedSitesStr;
            user.AllowedWarehouseIds = allowedWhsStr;

            userRepo.Update(user);

            // Update user roles
            var existingRoles = user.UserRoles.ToList();
            foreach (var er in existingRoles)
            {
                _unitOfWork.Repository<UserRole>().Delete(er);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (updateDto.RoleIds != null)
            {
                foreach (var roleId in updateDto.RoleIds)
                {
                    var userRole = new UserRole { UserId = user.Id, RoleId = roleId };
                    await _unitOfWork.Repository<UserRole>().AddAsync(userRole, cancellationToken);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var userRepo = _unitOfWork.Repository<User>();
            var user = await userRepo.GetByIdAsync(id, cancellationToken, u => u.UserRoles);
            if (user == null) return NotFound();

            // Delete associated user roles
            foreach (var ur in user.UserRoles)
            {
                _unitOfWork.Repository<UserRole>().Delete(ur);
            }

            userRepo.Delete(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return NoContent();
        }
    }
}
