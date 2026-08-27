using AutoMapper;
using Application.DTOs;
using Application.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers
{
    public abstract class CrudControllerBase<TEntity, TDto, TCreateDto> : ControllerBase 
        where TEntity : BaseEntity 
        where TDto : class 
        where TCreateDto : class
    {
        protected readonly IUnitOfWork UnitOfWork;
        protected readonly IMapper Mapper;

        protected CrudControllerBase(IUnitOfWork unitOfWork, IMapper mapper)
        {
            UnitOfWork = unitOfWork;
            Mapper = mapper;
        }

        protected Guid? CurrentUserSiteId
        {
            get
            {
                if (Request.Headers.TryGetValue("X-Site-Id", out var hVal) && Guid.TryParse(hVal.FirstOrDefault(), out var hGuid) && hGuid != Guid.Empty)
                    return hGuid;

                var claim = User.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out var g) && g != Guid.Empty);
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        protected Guid? CurrentUserWarehouseId
        {
            get
            {
                if (Request.Headers.TryGetValue("X-Warehouse-Id", out var hVal) && Guid.TryParse(hVal.FirstOrDefault(), out var hGuid) && hGuid != Guid.Empty)
                    return hGuid;

                var claim = User.Claims
                    .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out var g) && g != Guid.Empty);
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        protected bool IsSuperAdmin
        {
            get
            {
                return User.IsInRole("Super Admin") || 
                       User.IsInRole("System Administrator") || 
                       User.Claims.Any(c => (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role" || c.Type == "roles") && 
                                            (c.Value.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) || c.Value.Equals("System Administrator", StringComparison.OrdinalIgnoreCase)));
            }
        }

        protected Guid? CurrentUserId
        {
            get
            {
                var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        protected List<Guid> AllowedSiteIds
        {
            get
            {
                var guids = new HashSet<Guid>();
                foreach (var claim in User.Claims.Where(c => c.Type == "sites" || c.Type == "siteId" || c.Type == "site_id" || c.Type == "allowed_site_ids" || c.Type == "allowed_site_ids_csv"))
                {
                    foreach (var part in claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Guid.TryParse(part.Trim(), out var g) && g != Guid.Empty)
                            guids.Add(g);
                    }
                }
                return guids.ToList();
            }
        }

        protected List<Guid> AllowedWarehouseIds
        {
            get
            {
                var guids = new HashSet<Guid>();
                foreach (var claim in User.Claims.Where(c => c.Type == "warehouses" || c.Type == "warehouseId" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids" || c.Type == "allowed_warehouse_ids_csv"))
                {
                    foreach (var part in claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Guid.TryParse(part.Trim(), out var g) && g != Guid.Empty)
                            guids.Add(g);
                    }
                }
                return guids.ToList();
            }
        }

        protected Expression<Func<TEntity, bool>>? BuildCombinedFilter(Guid? querySiteId = null, Guid? queryWarehouseId = null)
        {
            var targetWhId = queryWarehouseId ?? CurrentUserWarehouseId;
            var targetSiteId = querySiteId ?? CurrentUserSiteId;

            List<Expression> predicates = new();
            var parameter = Expression.Parameter(typeof(TEntity), "e");

            // 1. WarehouseId Filter (Takes precedence if in Warehouse context)
            if (targetWhId.HasValue)
            {
                var whProp = typeof(TEntity).GetProperty("WarehouseId");
                if (whProp != null)
                {
                    Expression left = Expression.Property(parameter, whProp);
                    Expression right = whProp.PropertyType == typeof(Guid?) 
                        ? Expression.Constant(targetWhId, typeof(Guid?)) 
                        : Expression.Constant(targetWhId.Value, typeof(Guid));
                    predicates.Add(Expression.Equal(left, right));
                }
            }
            // 2. SiteId Filter (Applies when Site context active)
            else if (targetSiteId.HasValue)
            {
                var siteProp = typeof(TEntity).GetProperty("SiteId");
                if (siteProp != null)
                {
                    Expression left = Expression.Property(parameter, siteProp);
                    Expression right = siteProp.PropertyType == typeof(Guid?) 
                        ? Expression.Constant(targetSiteId, typeof(Guid?)) 
                        : Expression.Constant(targetSiteId.Value, typeof(Guid));
                    predicates.Add(Expression.Equal(left, right));
                }
            }
            else if (!IsSuperAdmin)
            {
                // In Global context without a specific site/warehouse selected, filter by user's allowed sites/warehouses or user's own records
                var allowedWhs = AllowedWarehouseIds;
                var allowedSites = AllowedSiteIds;
                var whProp = typeof(TEntity).GetProperty("WarehouseId");
                var siteProp = typeof(TEntity).GetProperty("SiteId");

                if (allowedWhs.Any() && whProp != null)
                {
                    var containsMethod = typeof(List<Guid>).GetMethod("Contains", new[] { typeof(Guid) })!;
                    if (whProp.PropertyType == typeof(Guid?))
                    {
                        var whVal = Expression.Property(parameter, whProp);
                        var hasValue = Expression.Property(whVal, "HasValue");
                        var value = Expression.Property(whVal, "Value");
                        var inList = Expression.Call(Expression.Constant(allowedWhs), containsMethod, value);
                        predicates.Add(Expression.AndAlso(hasValue, inList));
                    }
                    else if (whProp.PropertyType == typeof(Guid))
                    {
                        var inList = Expression.Call(Expression.Constant(allowedWhs), containsMethod, Expression.Property(parameter, whProp));
                        predicates.Add(inList);
                    }
                }
                else if (allowedSites.Any() && siteProp != null)
                {
                    var containsMethod = typeof(List<Guid>).GetMethod("Contains", new[] { typeof(Guid) })!;
                    if (siteProp.PropertyType == typeof(Guid?))
                    {
                        var siteVal = Expression.Property(parameter, siteProp);
                        var hasValue = Expression.Property(siteVal, "HasValue");
                        var value = Expression.Property(siteVal, "Value");
                        var inList = Expression.Call(Expression.Constant(allowedSites), containsMethod, value);
                        predicates.Add(Expression.AndAlso(hasValue, inList));
                    }
                    else if (siteProp.PropertyType == typeof(Guid))
                    {
                        var inList = Expression.Call(Expression.Constant(allowedSites), containsMethod, Expression.Property(parameter, siteProp));
                        predicates.Add(inList);
                    }
                }
            }

            if (!predicates.Any()) return null;

            Expression combined = predicates[0];
            for (int i = 1; i < predicates.Count; i++)
            {
                combined = Expression.AndAlso(combined, predicates[i]);
            }

            return Expression.Lambda<Func<TEntity, bool>>(combined, parameter);
        }

        protected Expression<Func<TEntity, bool>>? GetSiteFilterExpression()
        {
            return BuildCombinedFilter();
        }

        protected bool EnforceContextRestriction(TEntity entity)
        {
            if (IsSuperAdmin && !CurrentUserWarehouseId.HasValue && !CurrentUserSiteId.HasValue) return true;

            var whId = CurrentUserWarehouseId;
            if (whId.HasValue)
            {
                var whProperty = typeof(TEntity).GetProperty("WarehouseId");
                if (whProperty != null)
                {
                    var entityWhId = whProperty.GetValue(entity);
                    if (entityWhId != null)
                    {
                        Guid? entityWhGuid = entityWhId as Guid?;
                        if (entityWhGuid == null && entityWhId is Guid g) entityWhGuid = g;
                        if (entityWhGuid.HasValue && entityWhGuid.Value != whId.Value)
                        {
                            return false;
                        }
                    }
                }
            }

            var siteId = CurrentUserSiteId;
            if (siteId.HasValue && !whId.HasValue)
            {
                var property = typeof(TEntity).GetProperty("SiteId");
                if (property != null)
                {
                    var entitySiteId = property.GetValue(entity);
                    if (entitySiteId != null)
                    {
                        Guid? entitySiteGuid = entitySiteId as Guid?;
                        if (entitySiteGuid == null && entitySiteId is Guid g) entitySiteGuid = g;
                        if (entitySiteGuid.HasValue && entitySiteGuid.Value != siteId.Value)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        [HttpGet]
        public virtual async Task<ActionResult<IEnumerable<TDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<TEntity>();
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(page, size, search, filter, null, cancellationToken);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<TDto>>(items));
        }

        [HttpGet("{id:guid}")]
        public virtual async Task<ActionResult<TDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var entity = await UnitOfWork.Repository<TEntity>().GetByIdAsync(id, cancellationToken);
            if (entity == null) return NotFound();

            if (!EnforceContextRestriction(entity))
            {
                return NotFound();
            }

            return Ok(Mapper.Map<TDto>(entity));
        }

        [HttpPost]
        public virtual async Task<ActionResult<TDto>> Create([FromBody] TCreateDto createDto, CancellationToken cancellationToken)
        {
            var entity = Mapper.Map<TEntity>(createDto);

            var whId = CurrentUserWarehouseId;
            if (whId.HasValue)
            {
                var whProp = typeof(TEntity).GetProperty("WarehouseId");
                if (whProp != null)
                {
                    var currentVal = whProp.GetValue(entity);
                    if (currentVal == null || (currentVal is Guid g && g == Guid.Empty))
                    {
                        if (whProp.PropertyType == typeof(Guid?)) whProp.SetValue(entity, whId);
                        else whProp.SetValue(entity, whId.Value);
                    }
                }
            }

            var siteId = CurrentUserSiteId;
            if (siteId.HasValue && !whId.HasValue)
            {
                var property = typeof(TEntity).GetProperty("SiteId");
                if (property != null)
                {
                    var currentVal = property.GetValue(entity);
                    if (currentVal == null || (currentVal is Guid g && g == Guid.Empty))
                    {
                        if (property.PropertyType == typeof(Guid?)) property.SetValue(entity, siteId);
                        else property.SetValue(entity, siteId.Value);
                    }
                }
            }

            await UnitOfWork.Repository<TEntity>().AddAsync(entity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Mapper.Map<TDto>(entity));
        }

        [HttpPut("{id:guid}")]
        public virtual async Task<IActionResult> Update(Guid id, [FromBody] TCreateDto updateDto, CancellationToken cancellationToken)
        {
            var repo = UnitOfWork.Repository<TEntity>();
            var entity = await repo.GetByIdAsync(id, cancellationToken);
            if (entity == null) return NotFound();

            if (!EnforceContextRestriction(entity))
            {
                return NotFound();
            }

            Mapper.Map(updateDto, entity);

            var whId = CurrentUserWarehouseId;
            if (whId.HasValue)
            {
                var whProp = typeof(TEntity).GetProperty("WarehouseId");
                if (whProp != null)
                {
                    var currentVal = whProp.GetValue(entity);
                    if (currentVal == null || (currentVal is Guid g && g == Guid.Empty))
                    {
                        if (whProp.PropertyType == typeof(Guid?)) whProp.SetValue(entity, whId);
                        else whProp.SetValue(entity, whId.Value);
                    }
                }
            }

            var siteId = CurrentUserSiteId;
            if (siteId.HasValue && !whId.HasValue)
            {
                var property = typeof(TEntity).GetProperty("SiteId");
                if (property != null)
                {
                    var currentVal = property.GetValue(entity);
                    if (currentVal == null || (currentVal is Guid g && g == Guid.Empty))
                    {
                        if (property.PropertyType == typeof(Guid?)) property.SetValue(entity, siteId);
                        else property.SetValue(entity, siteId.Value);
                    }
                }
            }

            repo.Update(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public virtual async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var repo = UnitOfWork.Repository<TEntity>();
            var entity = await repo.GetByIdAsync(id, cancellationToken);
            if (entity == null) return NotFound();

            if (!EnforceContextRestriction(entity))
            {
                return NotFound();
            }

            repo.Delete(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }



    [Authorize]
    [ApiController]
    [Route("api/roles")]
    public class RolesController : CrudControllerBase<Role, RoleDto, CreateRoleDto>
    {
        public RolesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<RoleDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var roles = await UnitOfWork.Repository<Role>().GetFilteredAsync(r => !r.IsDeleted, cancellationToken, r => r.RolePermissions);
            foreach (var r in roles)
            {
                foreach (var rp in r.RolePermissions)
                {
                    if (rp.Permission == null)
                    {
                        var perm = await UnitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId, cancellationToken);
                        if (perm != null) rp.Permission = perm;
                    }
                }
            }

            var dtos = Mapper.Map<List<RoleDto>>(roles);
            Response.Headers.Add("X-Total-Count", roles.Count.ToString());
            return Ok(dtos);
        }

        [HttpGet("{id:guid}")]
        public override async Task<ActionResult<RoleDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var role = await UnitOfWork.Repository<Role>().GetByIdAsync(id, cancellationToken, r => r.RolePermissions);
            if (role == null) return NotFound();

            foreach (var rp in role.RolePermissions)
            {
                if (rp.Permission == null)
                {
                    var perm = await UnitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId, cancellationToken);
                    if (perm != null) rp.Permission = perm;
                }
            }

            return Ok(Mapper.Map<RoleDto>(role));
        }

        public override async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleDto createDto, CancellationToken cancellationToken)
        {
            var role = Mapper.Map<Role>(createDto);
            await UnitOfWork.Repository<Role>().AddAsync(role, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var permId in createDto.PermissionIds)
            {
                await UnitOfWork.Repository<RolePermission>().AddAsync(new RolePermission { RoleId = role.Id, PermissionId = permId }, cancellationToken);
            }
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = role.Id }, Mapper.Map<RoleDto>(role));
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/permissions")]
    public class PermissionsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public PermissionsController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PermissionDto>>> GetAll(CancellationToken cancellationToken)
        {
            var permissions = await _unitOfWork.Repository<Permission>().GetAllAsync(cancellationToken);
            return Ok(_mapper.Map<List<PermissionDto>>(permissions));
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/sites")]
    public class SitesController : CrudControllerBase<Site, SiteDto, CreateSiteDto>
    {
        public SitesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<SiteDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<Site>();
            var allSites = await repo.GetFilteredAsync(s => !s.IsDeleted, cancellationToken);

            var userEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "email")?.Value ?? "";
            var userName = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name || c.Type == "unique_name" || c.Type == "username")?.Value ?? User.Identity?.Name ?? "";
            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "id")?.Value ?? "";

            var roles = User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
                .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(r => r.Trim())
                .ToList();

            var isSuperAdmin = User.IsInRole("Super Admin") || User.IsInRole("System Administrator") || User.IsInRole("SuperAdmin") ||
                roles.Any(r => r.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) || r.Equals("System Administrator", StringComparison.OrdinalIgnoreCase) || r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)) ||
                User.HasClaim(c => (c.Type == "allowed_site_ids" && c.Value == "ALL") || (c.Type == "sites" && c.Value == "GLOBAL_ALL_SITES"));

            var isSiteScopedRole = roles.Any(r =>
                r.Equals("Site Admin", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SiteAdmin", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Store Keeper", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("StoreKeeper", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Safety Inspector", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("SafetyInspector", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Operator", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("Viewer", StringComparison.OrdinalIgnoreCase)
            );

            var allowedSiteIds = User.Claims
                .Where(c => c.Type == "sites" || c.Type == "siteId" || c.Type == "site_id" || c.Type == "allowed_site_ids" || c.Type == "allowed_site_ids_csv")
                .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(v => Guid.TryParse(v.Trim(), out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue && g.Value != Guid.Empty)
                .Select(g => g!.Value)
                .Distinct()
                .ToHashSet();

            if (Request.Headers.TryGetValue("X-Site-Id", out var hSite) && Guid.TryParse(hSite.FirstOrDefault(), out var parsedHSite) && parsedHSite != Guid.Empty)
            {
                allowedSiteIds.Add(parsedHSite);
            }

            IEnumerable<Site> filtered;
            if (isSuperAdmin)
            {
                filtered = allSites.Where(s =>
                    !string.IsNullOrWhiteSpace(s.CreatedBy) && (
                        (!string.IsNullOrWhiteSpace(userEmail) && s.CreatedBy.Equals(userEmail, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(userName) && s.CreatedBy.Equals(userName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(userIdStr) && s.CreatedBy.Equals(userIdStr, StringComparison.OrdinalIgnoreCase))
                    )
                );
            }
            else
            {
                filtered = allSites.Where(s => allowedSiteIds.Contains(s.Id));
            }

            if (siteId.HasValue)
            {
                filtered = filtered.Where(s => s.Id == siteId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(s => (s.Name != null && s.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) || (s.Code != null && s.Code.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            var list = filtered.ToList();
            Response.Headers.Append("X-Total-Count", list.Count.ToString());
            return Ok(Mapper.Map<List<SiteDto>>(list));
        }

        [HttpPost]
        public override async Task<ActionResult<SiteDto>> Create([FromBody] CreateSiteDto createDto, CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<Site>();
            var existing = await repo.GetAllAsync(cancellationToken);
            var code = string.IsNullOrWhiteSpace(createDto.Code) ? "SITE-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper() : createDto.Code.Trim();

            if (existing.Any(s => s.Code != null && s.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            {
                code += "-" + Random.Shared.Next(100, 999);
            }

            var userEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "email")?.Value;
            var userName = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name || c.Type == "unique_name" || c.Type == "username")?.Value ?? User.Identity?.Name;
            var creator = !string.IsNullOrWhiteSpace(userEmail) ? userEmail : (!string.IsNullOrWhiteSpace(userName) ? userName : User.FindFirst("sub")?.Value);

            var entity = new Site
            {
                Code = code,
                Name = createDto.Name,
                Address = createDto.Address,
                CreatedBy = creator,
                CreatedOn = DateTime.UtcNow
            };

            await repo.AddAsync(entity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Mapper.Map<SiteDto>(entity));
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/warehouses")]
    public class WarehousesController : CrudControllerBase<Warehouse, WarehouseDto, CreateWarehouseDto>
    {
        public WarehousesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<WarehouseDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<Warehouse>();
            var allWarehouses = await repo.GetFilteredAsync(w => !w.IsDeleted, cancellationToken);

            var userEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "email")?.Value ?? "";
            var userName = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name || c.Type == "unique_name" || c.Type == "username")?.Value ?? User.Identity?.Name ?? "";
            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "id")?.Value ?? "";

            var roles = User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
                .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(r => r.Trim())
                .ToList();

            var isSuperAdmin = User.IsInRole("Super Admin") || User.IsInRole("System Administrator") || User.IsInRole("SuperAdmin") ||
                roles.Any(r => r.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) || r.Equals("System Administrator", StringComparison.OrdinalIgnoreCase) || r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase));

            var allowedWhIds = User.Claims
                .Where(c => c.Type == "warehouses" || c.Type == "warehouseId" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids" || c.Type == "allowed_warehouse_ids_csv")
                .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(v => Guid.TryParse(v.Trim(), out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue && g.Value != Guid.Empty)
                .Select(g => g!.Value)
                .Distinct()
                .ToHashSet();

            if (Request.Headers.TryGetValue("X-Warehouse-Id", out var hWh) && Guid.TryParse(hWh.FirstOrDefault(), out var parsedHWh) && parsedHWh != Guid.Empty)
            {
                allowedWhIds.Add(parsedHWh);
            }

            IEnumerable<Warehouse> filtered;
            if (isSuperAdmin)
            {
                filtered = allWarehouses.Where(w =>
                    !string.IsNullOrWhiteSpace(w.CreatedBy) && (
                        (!string.IsNullOrWhiteSpace(userEmail) && w.CreatedBy.Equals(userEmail, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(userName) && w.CreatedBy.Equals(userName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(userIdStr) && w.CreatedBy.Equals(userIdStr, StringComparison.OrdinalIgnoreCase))
                    )
                );
            }
            else
            {
                filtered = allWarehouses.Where(w => allowedWhIds.Contains(w.Id));
            }

            if (warehouseId.HasValue)
            {
                filtered = filtered.Where(w => w.Id == warehouseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(w => (w.Name != null && w.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) || (w.Code != null && w.Code.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            var list = filtered.ToList();
            Response.Headers.Append("X-Total-Count", list.Count.ToString());
            return Ok(Mapper.Map<List<WarehouseDto>>(list));
        }

        [HttpPost]
        public override async Task<ActionResult<WarehouseDto>> Create([FromBody] CreateWarehouseDto createDto, CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<Warehouse>();
            var existing = await repo.GetAllAsync(cancellationToken);
            var code = string.IsNullOrWhiteSpace(createDto.Code) ? "WH-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper() : createDto.Code.Trim();

            if (existing.Any(w => w.Code != null && w.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            {
                code += "-" + Random.Shared.Next(100, 999);
            }

            var userEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "email")?.Value;
            var userName = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name || c.Type == "unique_name" || c.Type == "username")?.Value ?? User.Identity?.Name;
            var creator = !string.IsNullOrWhiteSpace(userEmail) ? userEmail : (!string.IsNullOrWhiteSpace(userName) ? userName : User.FindFirst("sub")?.Value);

            var entity = new Warehouse
            {
                Code = code,
                Name = createDto.Name,
                Address = createDto.Address,
                CreatedBy = creator,
                CreatedOn = DateTime.UtcNow
            };

            await repo.AddAsync(entity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return Ok(Mapper.Map<WarehouseDto>(entity));
        }

        [HttpGet("{id:guid}")]
        public override async Task<ActionResult<WarehouseDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var entity = await UnitOfWork.Repository<Warehouse>().GetByIdAsync(
                id,
                cancellationToken);
            if (entity == null) return NotFound();

            if (!EnforceContextRestriction(entity))
            {
                return NotFound();
            }

            return Ok(Mapper.Map<WarehouseDto>(entity));
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/zones")]
    public class ZonesController : CrudControllerBase<Zone, ZoneDto, CreateZoneDto>
    {
        public ZonesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<ZoneDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<Zone>();
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(
                page,
                size,
                search,
                filter,
                null,
                cancellationToken,
                x => x.Warehouse);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<ZoneDto>>(items));
        }

        [HttpGet("{id:guid}")]
        public override async Task<ActionResult<ZoneDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var entity = await UnitOfWork.Repository<Zone>().GetByIdAsync(
                id,
                cancellationToken,
                x => x.Warehouse);
            if (entity == null) return NotFound();

            if (!EnforceContextRestriction(entity))
            {
                return NotFound();
            }

            return Ok(Mapper.Map<ZoneDto>(entity));
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/locations")]
    public class LocationsController : CrudControllerBase<Location, LocationDto, CreateLocationDto>
    {
        private readonly Infrastructure.Persistence.Context.AssetTrackingDbContext _ctx;

        public LocationsController(IUnitOfWork unitOfWork, IMapper mapper,
            Infrastructure.Persistence.Context.AssetTrackingDbContext ctx) : base(unitOfWork, mapper)
        {
            _ctx = ctx;
        }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<LocationDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _ctx.Locations
                .Include(l => l.Zone)
                    .ThenInclude(z => z.Warehouse)
                .Where(l => !l.IsDeleted);

            var filter = BuildCombinedFilter(siteId, warehouseId);
            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(l => l.Name.Contains(search) || l.Code.Contains(search));

            int total = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<LocationDto>>(items));
        }

        [HttpGet("{id:guid}")]
        public override async Task<ActionResult<LocationDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var entity = await _ctx.Locations
                .Include(l => l.Zone)
                    .ThenInclude(z => z.Warehouse)
                .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, cancellationToken);
            if (entity == null) return NotFound();

            if (!EnforceContextRestriction(entity))
            {
                return NotFound();
            }

            return Ok(Mapper.Map<LocationDto>(entity));
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/manufacturers")]
    public class ManufacturersController : CrudControllerBase<Manufacturer, ManufacturerDto, CreateManufacturerDto>
    {
        public ManufacturersController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }

    [Authorize]
    [ApiController]
    [Route("api/vendors")]
    public class VendorsController : CrudControllerBase<Vendor, VendorDto, CreateVendorDto>
    {
        public VendorsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }

    [Authorize]
    [ApiController]
    [Route("api/categories")]
    public class CategoriesController : CrudControllerBase<AssetCategory, AssetCategoryDto, CreateAssetCategoryDto>
    {
        public CategoriesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }

    [Authorize]
    [ApiController]
    [Route("api/rfidtags")]
    public class RFIDTagsController : CrudControllerBase<RFIDTag, RFIDTagDto, CreateRFIDTagDto>
    {
        public RFIDTagsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<RFIDTagDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<RFIDTag>();
            var targetWhId = warehouseId ?? CurrentUserWarehouseId;
            var targetSiteId = siteId ?? CurrentUserSiteId;

            Expression<Func<RFIDTag, bool>>? filter = null;

            if (targetWhId.HasValue)
            {
                filter = t => (t.Asset != null && t.Asset.WarehouseId == targetWhId.Value) || t.AssetId == null;
            }
            else if (targetSiteId.HasValue)
            {
                filter = t => (t.Asset != null && t.Asset.SiteId == targetSiteId.Value) || t.AssetId == null;
            }
            else if (!IsSuperAdmin)
            {
                var allowedWhs = AllowedWarehouseIds;
                var allowedSites = AllowedSiteIds;
                var currentUserIdStr = CurrentUserId?.ToString();
                var currentEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? User.Identity?.Name;

                if (allowedWhs.Any())
                {
                    filter = t => (t.Asset != null && t.Asset.WarehouseId.HasValue && allowedWhs.Contains(t.Asset.WarehouseId.Value))
                               || t.AssetId == null;
                }
                else if (allowedSites.Any())
                {
                    filter = t => (t.Asset != null && t.Asset.SiteId.HasValue && allowedSites.Contains(t.Asset.SiteId.Value))
                               || t.AssetId == null;
                }
                else
                {
                    filter = t => (t.Asset != null && (t.Asset.CreatedBy == currentUserIdStr || (currentEmail != null && t.Asset.CreatedBy == currentEmail)))
                               || t.AssetId == null;
                }
            }

            var (items, total) = await repo.GetPagedAsync(page, size, search, filter, null, cancellationToken, t => t.Asset!);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<RFIDTagDto>>(items));
        }

        [HttpPost]
        public override async Task<ActionResult<RFIDTagDto>> Create([FromBody] CreateRFIDTagDto createDto, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(createDto.EpcCode))
            {
                return BadRequest("EPC Code is required.");
            }

            var epc = createDto.EpcCode.Trim();
            var repo = UnitOfWork.Repository<RFIDTag>();

            Guid? validAssetId = null;
            if (createDto.AssetId.HasValue && createDto.AssetId.Value != Guid.Empty)
            {
                var asset = await UnitOfWork.Repository<Asset>().GetByIdAsync(createDto.AssetId.Value, cancellationToken);
                if (asset != null && !asset.IsDeleted)
                {
                    validAssetId = createDto.AssetId.Value;
                    asset.RfidTag = epc;
                    UnitOfWork.Repository<Asset>().Update(asset);
                }
            }

            var existingTags = await repo.GetFilteredAsync(t => t.EpcCode == epc, cancellationToken);
            var existing = existingTags.FirstOrDefault();

            if (existing != null)
            {
                existing.IsDeleted = false;
                existing.DeletedOn = null;
                existing.DeletedBy = null;
                existing.AssetId = validAssetId;
                if (!string.IsNullOrWhiteSpace(createDto.TidCode))
                    existing.TidCode = createDto.TidCode.Trim();
                if (!string.IsNullOrEmpty(createDto.Status) && Enum.TryParse<TagStatus>(createDto.Status, true, out var parsedStatus))
                    existing.Status = parsedStatus;

                repo.Update(existing);
                await UnitOfWork.SaveChangesAsync(cancellationToken);
                return Ok(Mapper.Map<RFIDTagDto>(existing));
            }

            var entity = new RFIDTag
            {
                Id = Guid.NewGuid(),
                EpcCode = epc,
                TidCode = createDto.TidCode?.Trim(),
                Status = (!string.IsNullOrEmpty(createDto.Status) && Enum.TryParse<TagStatus>(createDto.Status, true, out var st)) ? st : TagStatus.Active,
                AssetId = validAssetId,
                CreatedOn = DateTime.UtcNow
            };

            await repo.AddAsync(entity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Mapper.Map<RFIDTagDto>(entity));
        }

        [HttpPut("{id:guid}")]
        public override async Task<IActionResult> Update(Guid id, [FromBody] CreateRFIDTagDto updateDto, CancellationToken cancellationToken)
        {
            var repo = UnitOfWork.Repository<RFIDTag>();
            var entity = await repo.GetByIdAsync(id, cancellationToken);
            if (entity == null) return NotFound();

            var epc = !string.IsNullOrWhiteSpace(updateDto.EpcCode) ? updateDto.EpcCode.Trim() : entity.EpcCode;

            Guid? validAssetId = null;
            if (updateDto.AssetId.HasValue && updateDto.AssetId.Value != Guid.Empty)
            {
                var asset = await UnitOfWork.Repository<Asset>().GetByIdAsync(updateDto.AssetId.Value, cancellationToken);
                if (asset != null && !asset.IsDeleted)
                {
                    validAssetId = updateDto.AssetId.Value;
                    asset.RfidTag = epc;
                    UnitOfWork.Repository<Asset>().Update(asset);
                }
            }

            if (!string.IsNullOrWhiteSpace(updateDto.EpcCode))
                entity.EpcCode = epc;
            if (updateDto.TidCode != null)
                entity.TidCode = updateDto.TidCode.Trim();
            entity.AssetId = validAssetId;
            if (!string.IsNullOrEmpty(updateDto.Status) && Enum.TryParse<TagStatus>(updateDto.Status, true, out var parsedStatus))
                entity.Status = parsedStatus;

            repo.Update(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/barcodes")]
    public class BarcodesController : CrudControllerBase<Barcode, BarcodeDto, CreateBarcodeDto>
    {
        public BarcodesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<BarcodeDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<Barcode>();
            var targetWhId = warehouseId ?? CurrentUserWarehouseId;
            var targetSiteId = siteId ?? CurrentUserSiteId;

            Expression<Func<Barcode, bool>>? filter = null;

            if (targetWhId.HasValue)
            {
                filter = b => (b.Asset != null && b.Asset.WarehouseId == targetWhId.Value) || b.AssetId == null;
            }
            else if (targetSiteId.HasValue)
            {
                filter = b => (b.Asset != null && b.Asset.SiteId == targetSiteId.Value) || b.AssetId == null;
            }
            else if (!IsSuperAdmin)
            {
                var allowedWhs = AllowedWarehouseIds;
                var allowedSites = AllowedSiteIds;
                var currentUserIdStr = CurrentUserId?.ToString();
                var currentEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? User.Identity?.Name;

                if (allowedWhs.Any())
                {
                    filter = b => (b.Asset != null && b.Asset.WarehouseId.HasValue && allowedWhs.Contains(b.Asset.WarehouseId.Value))
                               || b.AssetId == null;
                }
                else if (allowedSites.Any())
                {
                    filter = b => (b.Asset != null && b.Asset.SiteId.HasValue && allowedSites.Contains(b.Asset.SiteId.Value))
                               || b.AssetId == null;
                }
                else
                {
                    filter = b => (b.Asset != null && (b.Asset.CreatedBy == currentUserIdStr || (currentEmail != null && b.Asset.CreatedBy == currentEmail)))
                               || b.AssetId == null;
                }
            }

            var (items, total) = await repo.GetPagedAsync(page, size, search, filter, null, cancellationToken, b => b.Asset!);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<BarcodeDto>>(items));
        }

        [HttpPost]
        public override async Task<ActionResult<BarcodeDto>> Create([FromBody] CreateBarcodeDto createDto, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(createDto.BarcodeValue))
            {
                return BadRequest("Barcode Value is required.");
            }

            var barcodeVal = createDto.BarcodeValue.Trim();
            var repo = UnitOfWork.Repository<Barcode>();

            Guid? validAssetId = null;
            if (createDto.AssetId.HasValue && createDto.AssetId.Value != Guid.Empty)
            {
                var asset = await UnitOfWork.Repository<Asset>().GetByIdAsync(createDto.AssetId.Value, cancellationToken);
                if (asset != null && !asset.IsDeleted)
                {
                    validAssetId = createDto.AssetId.Value;
                    asset.Barcode = barcodeVal;
                    UnitOfWork.Repository<Asset>().Update(asset);
                }
            }

            var existingBarcodes = await repo.GetFilteredAsync(b => b.BarcodeValue == barcodeVal, cancellationToken);
            var existing = existingBarcodes.FirstOrDefault();

            if (existing != null)
            {
                existing.IsDeleted = false;
                existing.DeletedOn = null;
                existing.DeletedBy = null;
                existing.AssetId = validAssetId;
                if (!string.IsNullOrWhiteSpace(createDto.Format))
                    existing.Format = createDto.Format.Trim();
                if (createDto.IsActive.HasValue)
                    existing.IsActive = createDto.IsActive.Value;

                repo.Update(existing);
                await UnitOfWork.SaveChangesAsync(cancellationToken);
                return Ok(Mapper.Map<BarcodeDto>(existing));
            }

            var entity = new Barcode
            {
                Id = Guid.NewGuid(),
                BarcodeValue = barcodeVal,
                Format = string.IsNullOrWhiteSpace(createDto.Format) ? "Code128" : createDto.Format.Trim(),
                IsActive = createDto.IsActive ?? true,
                AssetId = validAssetId,
                CreatedOn = DateTime.UtcNow
            };

            await repo.AddAsync(entity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Mapper.Map<BarcodeDto>(entity));
        }

        [HttpPut("{id:guid}")]
        public override async Task<IActionResult> Update(Guid id, [FromBody] CreateBarcodeDto updateDto, CancellationToken cancellationToken)
        {
            var repo = UnitOfWork.Repository<Barcode>();
            var entity = await repo.GetByIdAsync(id, cancellationToken);
            if (entity == null) return NotFound();

            var barcodeVal = !string.IsNullOrWhiteSpace(updateDto.BarcodeValue) ? updateDto.BarcodeValue.Trim() : entity.BarcodeValue;

            Guid? validAssetId = null;
            if (updateDto.AssetId.HasValue && updateDto.AssetId.Value != Guid.Empty)
            {
                var asset = await UnitOfWork.Repository<Asset>().GetByIdAsync(updateDto.AssetId.Value, cancellationToken);
                if (asset != null && !asset.IsDeleted)
                {
                    validAssetId = updateDto.AssetId.Value;
                    asset.Barcode = barcodeVal;
                    UnitOfWork.Repository<Asset>().Update(asset);
                }
            }

            if (!string.IsNullOrWhiteSpace(updateDto.BarcodeValue))
                entity.BarcodeValue = barcodeVal;
            if (!string.IsNullOrWhiteSpace(updateDto.Format))
                entity.Format = updateDto.Format.Trim();
            entity.AssetId = validAssetId;
            if (updateDto.IsActive.HasValue)
                entity.IsActive = updateDto.IsActive.Value;

            repo.Update(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/gpsdevices")]
    public class GPSDevicesController : CrudControllerBase<GPSDevice, GPSDeviceDto, CreateGPSDeviceDto>
    {
        public GPSDevicesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<GPSDeviceDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<GPSDevice>();
            var targetWhId = warehouseId ?? CurrentUserWarehouseId;
            var targetSiteId = siteId ?? CurrentUserSiteId;

            Expression<Func<GPSDevice, bool>>? filter = null;

            if (targetWhId.HasValue)
            {
                filter = g => (g.Asset != null && g.Asset.WarehouseId == targetWhId.Value) || g.AssetId == null;
            }
            else if (targetSiteId.HasValue)
            {
                filter = g => (g.Asset != null && g.Asset.SiteId == targetSiteId.Value) || g.AssetId == null;
            }
            else if (!IsSuperAdmin)
            {
                var allowedWhs = AllowedWarehouseIds;
                var allowedSites = AllowedSiteIds;
                var currentUserIdStr = CurrentUserId?.ToString();
                var currentEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? User.Identity?.Name;

                if (allowedWhs.Any())
                {
                    filter = g => (g.Asset != null && g.Asset.WarehouseId.HasValue && allowedWhs.Contains(g.Asset.WarehouseId.Value))
                               || g.AssetId == null;
                }
                else if (allowedSites.Any())
                {
                    filter = g => (g.Asset != null && g.Asset.SiteId.HasValue && allowedSites.Contains(g.Asset.SiteId.Value))
                               || g.AssetId == null;
                }
                else
                {
                    filter = g => (g.Asset != null && (g.Asset.CreatedBy == currentUserIdStr || (currentEmail != null && g.Asset.CreatedBy == currentEmail)))
                               || g.AssetId == null;
                }
            }

            var (items, total) = await repo.GetPagedAsync(page, size, search, filter, null, cancellationToken, g => g.Asset!);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<GPSDeviceDto>>(items));
        }

        [HttpPost]
        public override async Task<ActionResult<GPSDeviceDto>> Create([FromBody] CreateGPSDeviceDto createDto, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(createDto.Imei))
            {
                return BadRequest("IMEI is required.");
            }

            var imei = createDto.Imei.Trim();
            var repo = UnitOfWork.Repository<GPSDevice>();

            Guid? validAssetId = null;
            if (createDto.AssetId.HasValue && createDto.AssetId.Value != Guid.Empty)
            {
                var asset = await UnitOfWork.Repository<Asset>().GetByIdAsync(createDto.AssetId.Value, cancellationToken);
                if (asset != null && !asset.IsDeleted)
                {
                    validAssetId = createDto.AssetId.Value;
                    asset.GpsId = imei;
                    UnitOfWork.Repository<Asset>().Update(asset);
                }
            }

            var existingDevices = await repo.GetFilteredAsync(g => g.Imei == imei, cancellationToken);
            var existing = existingDevices.FirstOrDefault();

            if (existing != null)
            {
                existing.IsDeleted = false;
                existing.DeletedOn = null;
                existing.DeletedBy = null;
                existing.AssetId = validAssetId;
                if (!string.IsNullOrWhiteSpace(createDto.SimNumber))
                    existing.SimNumber = createDto.SimNumber.Trim();
                if (!string.IsNullOrEmpty(createDto.Status) && Enum.TryParse<DeviceStatus>(createDto.Status, true, out var parsedStatus))
                    existing.Status = parsedStatus;

                repo.Update(existing);
                await UnitOfWork.SaveChangesAsync(cancellationToken);

                await SyncVehicleForGpsDeviceAsync(existing, cancellationToken);

                return Ok(Mapper.Map<GPSDeviceDto>(existing));
            }

            var entity = new GPSDevice
            {
                Id = Guid.NewGuid(),
                Imei = imei,
                SimNumber = createDto.SimNumber?.Trim(),
                BatteryLevel = 100,
                Status = (!string.IsNullOrEmpty(createDto.Status) && Enum.TryParse<DeviceStatus>(createDto.Status, true, out var st)) ? st : DeviceStatus.Online,
                AssetId = validAssetId,
                CreatedOn = DateTime.UtcNow
            };

            await repo.AddAsync(entity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            await SyncVehicleForGpsDeviceAsync(entity, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Mapper.Map<GPSDeviceDto>(entity));
        }

        [HttpPut("{id:guid}")]
        public override async Task<IActionResult> Update(Guid id, [FromBody] CreateGPSDeviceDto updateDto, CancellationToken cancellationToken)
        {
            var repo = UnitOfWork.Repository<GPSDevice>();
            var entity = await repo.GetByIdAsync(id, cancellationToken);
            if (entity == null) return NotFound();

            var imei = !string.IsNullOrWhiteSpace(updateDto.Imei) ? updateDto.Imei.Trim() : entity.Imei;

            Guid? validAssetId = null;
            if (updateDto.AssetId.HasValue && updateDto.AssetId.Value != Guid.Empty)
            {
                var asset = await UnitOfWork.Repository<Asset>().GetByIdAsync(updateDto.AssetId.Value, cancellationToken);
                if (asset != null && !asset.IsDeleted)
                {
                    validAssetId = updateDto.AssetId.Value;
                    asset.GpsId = imei;
                    UnitOfWork.Repository<Asset>().Update(asset);
                }
            }

            if (!string.IsNullOrWhiteSpace(updateDto.Imei))
                entity.Imei = imei;
            if (updateDto.SimNumber != null)
                entity.SimNumber = updateDto.SimNumber.Trim();
            entity.AssetId = validAssetId;
            if (!string.IsNullOrEmpty(updateDto.Status) && Enum.TryParse<DeviceStatus>(updateDto.Status, true, out var parsedStatus))
                entity.Status = parsedStatus;

            repo.Update(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            await SyncVehicleForGpsDeviceAsync(entity, cancellationToken);

            return NoContent();
        }

        private async Task SyncVehicleForGpsDeviceAsync(GPSDevice device, CancellationToken cancellationToken)
        {
            try
            {
                var vehicleRepo = UnitOfWork.Repository<Vehicle>();
                var vehicles = await vehicleRepo.GetFilteredAsync(v => v.DeviceNum == device.Imei, cancellationToken);
                var vehicle = vehicles.FirstOrDefault();
                if (vehicle == null)
                {
                    var asset = device.AssetId.HasValue ? await UnitOfWork.Repository<Asset>().GetByIdAsync(device.AssetId.Value, cancellationToken) : null;
                    var newVehicle = new Vehicle
                    {
                        Id = Guid.NewGuid(),
                        DeviceNum = device.Imei,
                        RegName = asset?.Name ?? $"GPS Tracker {device.Imei.Substring(Math.Max(0, device.Imei.Length - 4))}",
                        Status = "Online",
                        Lat = 18.620321 + (new Random().NextDouble() - 0.5) * 0.005,
                        Lon = 73.856742 + (new Random().NextDouble() - 0.5) * 0.005,
                        Speed = 15,
                        Direction = 90,
                        Battery = device.BatteryLevel > 0 ? device.BatteryLevel : 100,
                        GpsTime = DateTime.UtcNow,
                        UpdateTime = DateTime.UtcNow,
                        CreatedOn = DateTime.UtcNow
                    };
                    await vehicleRepo.AddAsync(newVehicle, cancellationToken);
                    await UnitOfWork.SaveChangesAsync(cancellationToken);
                }
                else if (vehicle.IsDeleted)
                {
                    vehicle.IsDeleted = false;
                    vehicle.DeletedOn = null;
                    vehicle.DeletedBy = null;
                    vehicleRepo.Update(vehicle);
                    await UnitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            catch { }
        }
    }



    [Authorize]
    [ApiController]
    [Route("api/handhelds")]
    public class HandheldDevicesController : CrudControllerBase<HandheldDevice, HandheldDeviceDto, CreateHandheldDeviceDto>
    {
        public HandheldDevicesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }

    [Authorize]
    [ApiController]
    [Route("api/assignments")]
    public class AssetAssignmentsController : CrudControllerBase<AssetAssignment, AssetAssignmentDto, CreateAssetAssignmentDto>
    {
        public AssetAssignmentsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<AssetAssignmentDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var targetSiteId = siteId ?? CurrentUserSiteId;
            var targetWhId = warehouseId ?? CurrentUserWarehouseId;

            var repo = UnitOfWork.Repository<AssetAssignment>();
            var (items, total) = await repo.GetPagedAsync(
                page,
                size,
                search,
                null,
                null,
                cancellationToken,
                x => x.Asset,
                x => x.AssignedToUser);

            if (targetWhId.HasValue)
            {
                items = items.Where(x => x.Asset != null && x.Asset.WarehouseId == targetWhId.Value).ToList();
            }
            else if (targetSiteId.HasValue)
            {
                items = items.Where(x => x.Asset != null && x.Asset.SiteId == targetSiteId.Value).ToList();
            }

            var dtos = Mapper.Map<List<AssetAssignmentDto>>(items);

            // Fetch AssetMovements so handheld/reader checkouts & checkins appear in assignments API & UI (excluding general inventory scans)
            var movementRepo = UnitOfWork.Repository<AssetMovement>();
            var movements = await movementRepo.GetFilteredAsync(
                m => m.MovementType != null &&
                     m.MovementType != "RFIDScan" &&
                     m.MovementType != "ScanInventory" &&
                     m.MovementType != "HandheldInventory" &&
                     m.MovementType != "HandheldScan",
                cancellationToken,
                m => m.Asset,
                m => m.HandheldDevice,
                m => m.DestinationLocation,
                m => m.SourceLocation);

            if (targetWhId.HasValue)
            {
                movements = movements.Where(m => m.Asset != null && m.Asset.WarehouseId == targetWhId.Value).ToList();
            }
            else if (targetSiteId.HasValue)
            {
                movements = movements.Where(m => m.Asset != null && m.Asset.SiteId == targetSiteId.Value).ToList();
            }

            foreach (var m in movements.OrderByDescending(x => x.MovementDate))
            {
                if (dtos.Any(d => d.Id == m.Id || (d.AssetId == m.AssetId && Math.Abs((d.AssignedDate - m.MovementDate).TotalSeconds) < 60)))
                    continue;

                bool isFixedReaderScan = m.HandheldDevice == null && m.HandheldDeviceId == null;
                bool isAntenna1Exit = isFixedReaderScan && m.Remarks != null && (m.Remarks.Contains("Antenna #1") || m.Remarks.Contains("Antenna 1") || m.Remarks.ToLower().Contains("exit"));

                bool isExit = isAntenna1Exit ||
                              string.Equals(m.MovementType, "Checkout", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(m.MovementType, "Exit", StringComparison.OrdinalIgnoreCase) ||
                              (m.MovementType != null && m.MovementType.ToUpper().Contains("EXIT"));

                bool isReturned = !isExit && (
                                  string.Equals(m.MovementType, "Checkin", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(m.MovementType, "Return", StringComparison.OrdinalIgnoreCase) ||
                                  (m.MovementType != null && m.MovementType.ToUpper().Contains("CHECKIN")));

                var deviceName = m.HandheldDevice?.Name ?? (isFixedReaderScan ? "Warehouse Exit/Entry Door" : "C72 Handheld Reader");

                var remarks = m.Remarks ?? (isExit ? "Checked out via Fixed Reader Exit (Antenna 1)." : "Scanned automatically at RFID Reader.");

                dtos.Add(new AssetAssignmentDto
                {
                    Id = m.Id,
                    AssetId = m.AssetId,
                    AssetName = m.Asset?.Name ?? "Unknown Equipment",
                    AssetNumber = m.Asset?.AssetNumber ?? "AST-000",
                    AssignedToUserId = Guid.Empty,
                    AssignedToUsername = deviceName,
                    CustodianName = deviceName,
                    AssignedDate = m.MovementDate,
                    ExpectedReturnDate = isReturned ? null : m.MovementDate.AddDays(1),
                    ActualReturnDate = isReturned ? m.MovementDate : null,
                    Purpose = isExit ? "Fixed Reader Exit" : (m.MovementType ?? "Checkout"),
                    Status = isReturned ? "Returned" : "Active",
                    Notes = remarks
                });
            }

            var sortedDtos = dtos.OrderByDescending(d => d.AssignedDate).ToList();

            Response.Headers.Add("X-Total-Count", sortedDtos.Count.ToString());
            return Ok(sortedDtos);
        }

        [HttpGet("{id:guid}")]
        public override async Task<ActionResult<AssetAssignmentDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var entity = await UnitOfWork.Repository<AssetAssignment>().GetByIdAsync(
                id,
                cancellationToken,
                x => x.Asset,
                x => x.AssignedToUser);
            if (entity == null) return NotFound();

            if (!EnforceContextRestriction(entity))
            {
                return NotFound();
            }

            return Ok(Mapper.Map<AssetAssignmentDto>(entity));
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/transfers")]
    public class AssetTransfersController : CrudControllerBase<AssetTransfer, AssetTransferDto, CreateAssetTransferDto>
    {
        public AssetTransfersController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<AssetTransferDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<AssetTransfer>();
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(
                page,
                size,
                search,
                filter,
                null,
                cancellationToken,
                x => x.Asset,
                x => x.SourceSite,
                x => x.DestinationSite,
                x => x.SourceLocation,
                x => x.DestinationLocation,
                x => x.RequestedByUser,
                x => x.ApprovedByUser);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<AssetTransferDto>>(items));
        }

        [HttpGet("{id:guid}")]
        public override async Task<ActionResult<AssetTransferDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var entity = await UnitOfWork.Repository<AssetTransfer>().GetByIdAsync(
                id,
                cancellationToken,
                x => x.Asset,
                x => x.SourceSite,
                x => x.DestinationSite,
                x => x.SourceLocation,
                x => x.DestinationLocation,
                x => x.RequestedByUser,
                x => x.ApprovedByUser);
            if (entity == null) return NotFound();

            if (!EnforceContextRestriction(entity))
            {
                return NotFound();
            }

            return Ok(Mapper.Map<AssetTransferDto>(entity));
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/movements")]
    public class AssetMovementsController : CrudControllerBase<AssetMovement, AssetMovementDto, CreateAssetMovementDto>
    {
        public AssetMovementsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<AssetMovementDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<AssetMovement>();
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(
                page,
                size,
                search,
                filter,
                null,
                cancellationToken,
                x => x.Asset,
                x => x.SourceLocation,
                x => x.DestinationLocation,
                x => x.Reader,
                x => x.HandheldDevice);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<AssetMovementDto>>(items));
        }

        [HttpGet("{id:guid}")]
        public override async Task<ActionResult<AssetMovementDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var entity = await UnitOfWork.Repository<AssetMovement>().GetByIdAsync(
                id,
                cancellationToken,
                x => x.Asset,
                x => x.SourceLocation,
                x => x.DestinationLocation,
                x => x.Reader,
                x => x.HandheldDevice);
            if (entity == null) return NotFound();

            if (!EnforceContextRestriction(entity))
            {
                return NotFound();
            }

            return Ok(Mapper.Map<AssetMovementDto>(entity));
        }
    }



    [Authorize]
    [ApiController]
    [Route("api/notifications")]
    public class NotificationsController : CrudControllerBase<Notification, NotificationDto, NotificationDto>
    {
        public NotificationsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }

    [Authorize]
    [ApiController]
    [Route("api/alerts")]
    public class AlertsController : CrudControllerBase<Alert, AlertDto, AlertDto>
    {
        public AlertsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<AlertDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<Alert>();
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(page, size, search, filter, null, cancellationToken);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<AlertDto>>(items));
        }
    }

    [Authorize]
    [ApiController]
    [Route("api/settings")]
    public class SettingsController : CrudControllerBase<Settings, SettingsDto, CreateSettingsDto>
    {
        public SettingsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }

    [Authorize]
    [ApiController]
    [Route("api/scansessions")]
    public class ScanSessionsController : CrudControllerBase<ScanSession, ScanSessionDto, CreateScanSessionDto>
    {
        public ScanSessionsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }

        [HttpGet]
        public override async Task<ActionResult<IEnumerable<ScanSessionDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<ScanSession>();
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(
                page,
                size,
                search,
                filter,
                null,
                cancellationToken,
                x => x.Reader,
                x => x.HandheldDevice,
                x => x.ScanEvents);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<ScanSessionDto>>(items));
        }
    }
}
