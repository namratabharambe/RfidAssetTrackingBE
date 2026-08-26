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
            var allSites = await repo.GetAllAsync(cancellationToken);

            var userEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "email")?.Value ?? "";
            var userName = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name || c.Type == "unique_name" || c.Type == "username")?.Value ?? User.Identity?.Name ?? "";
            var identityLower = (userName + " " + userEmail).ToLower();
            var isDevamUser = identityLower.Contains("devam");

            var isSuperAdmin = User.IsInRole("Super Admin") || User.IsInRole("System Administrator") || User.Claims.Any(c => (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role" || c.Type == "roles") && (c.Value.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) || c.Value.Equals("System Administrator", StringComparison.OrdinalIgnoreCase)));

            var allowedSiteIds = User.Claims
                .Where(c => c.Type == "sites" || c.Type == "siteId" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue && g.Value != Guid.Empty)
                .Select(g => g!.Value)
                .Distinct()
                .ToHashSet();

            if (Request.Headers.TryGetValue("X-Site-Id", out var hSite) && Guid.TryParse(hSite.FirstOrDefault(), out var parsedHSite) && parsedHSite != Guid.Empty)
            {
                allowedSiteIds.Add(parsedHSite);
            }

            IEnumerable<Site> filtered = allSites;

            if (allowedSiteIds.Any())
            {
                filtered = filtered.Where(s => allowedSiteIds.Contains(s.Id));
            }
            else if (!isSuperAdmin)
            {
                filtered = new List<Site>();
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
            Response.Headers.Add("X-Total-Count", list.Count.ToString());
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

            var entity = new Site
            {
                Code = code,
                Name = createDto.Name,
                Address = createDto.Address
            };

            await repo.AddAsync(entity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            return Ok(Mapper.Map<SiteDto>(entity));
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

            var isSuperAdmin = User.IsInRole("Super Admin") || User.IsInRole("System Administrator") || User.Claims.Any(c => (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role" || c.Type == "roles") && (c.Value.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) || c.Value.Equals("System Administrator", StringComparison.OrdinalIgnoreCase)));

            var allowedSiteIds = User.Claims
                .Where(c => c.Type == "sites" || c.Type == "siteId" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue && g.Value != Guid.Empty)
                .Select(g => g!.Value)
                .Distinct()
                .ToHashSet();

            var allowedWhIds = User.Claims
                .Where(c => c.Type == "warehouses" || c.Type == "warehouseId" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue && g.Value != Guid.Empty)
                .Select(g => g!.Value)
                .Distinct()
                .ToHashSet();

            // Header overrides
            if (Request.Headers.TryGetValue("X-Warehouse-Id", out var hWh) && Guid.TryParse(hWh.FirstOrDefault(), out var parsedHWh) && parsedHWh != Guid.Empty)
            {
                allowedWhIds.Add(parsedHWh);
            }
            if (Request.Headers.TryGetValue("X-Site-Id", out var hSite) && Guid.TryParse(hSite.FirstOrDefault(), out var parsedHSite) && parsedHSite != Guid.Empty)
            {
                allowedSiteIds.Add(parsedHSite);
            }

            IEnumerable<Warehouse> filtered = allWarehouses;

            if (allowedWhIds.Any())
            {
                filtered = filtered.Where(w => allowedWhIds.Contains(w.Id));
            }
            else if (!isSuperAdmin)
            {
                filtered = new List<Warehouse>();
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
            Response.Headers.Add("X-Total-Count", list.Count.ToString());
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

            var entity = new Warehouse
            {
                Code = code,
                Name = createDto.Name,
                Address = createDto.Address
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
    }

    [Authorize]
    [ApiController]
    [Route("api/barcodes")]
    public class BarcodesController : CrudControllerBase<Barcode, BarcodeDto, CreateBarcodeDto>
    {
        public BarcodesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
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
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(page, size, search, filter, null, cancellationToken);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<GPSDeviceDto>>(items));
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
