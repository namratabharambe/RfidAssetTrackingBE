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
                var claim = User.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out _));
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        protected Expression<Func<TEntity, bool>>? BuildCombinedFilter(Guid? querySiteId = null, Guid? queryWarehouseId = null)
        {
            var targetSiteId = querySiteId ?? CurrentUserSiteId;
            var targetWhId = queryWarehouseId;

            List<Expression> predicates = new();
            var parameter = Expression.Parameter(typeof(TEntity), "e");

            // 1. SiteId Filter
            if (targetSiteId.HasValue)
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

            // 2. WarehouseId Filter
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

        protected bool EnforceSiteRestriction(TEntity entity)
        {
            var siteId = CurrentUserSiteId;
            if (!siteId.HasValue) return true;

            var property = typeof(TEntity).GetProperty("SiteId");
            if (property == null) return true;

            var entitySiteId = property.GetValue(entity);
            if (entitySiteId == null) return true;

            Guid? entitySiteGuid = entitySiteId as Guid?;
            if (entitySiteGuid == null && entitySiteId is Guid g) entitySiteGuid = g;

            if (entitySiteGuid.HasValue && entitySiteGuid.Value != siteId.Value)
            {
                return false;
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

            if (!EnforceSiteRestriction(entity))
            {
                return NotFound();
            }

            return Ok(Mapper.Map<TDto>(entity));
        }

        [HttpPost]
        public virtual async Task<ActionResult<TDto>> Create([FromBody] TCreateDto createDto, CancellationToken cancellationToken)
        {
            var entity = Mapper.Map<TEntity>(createDto);

            var siteId = CurrentUserSiteId;
            if (siteId.HasValue)
            {
                var property = typeof(TEntity).GetProperty("SiteId");
                if (property != null)
                {
                    if (property.PropertyType == typeof(Guid?))
                    {
                        property.SetValue(entity, siteId);
                    }
                    else
                    {
                        property.SetValue(entity, siteId.Value);
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

            if (!EnforceSiteRestriction(entity))
            {
                return NotFound();
            }

            Mapper.Map(updateDto, entity);

            var siteId = CurrentUserSiteId;
            if (siteId.HasValue)
            {
                var property = typeof(TEntity).GetProperty("SiteId");
                if (property != null)
                {
                    if (property.PropertyType == typeof(Guid?))
                    {
                        property.SetValue(entity, siteId);
                    }
                    else
                    {
                        property.SetValue(entity, siteId.Value);
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

            if (!EnforceSiteRestriction(entity))
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

        [AllowAnonymous]
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

        [AllowAnonymous]
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

        [AllowAnonymous]
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
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(page, size, search, filter, null, cancellationToken);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<SiteDto>>(items));
        }

        [AllowAnonymous]
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

        [AllowAnonymous]
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
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(
                page,
                size,
                search,
                filter,
                null,
                cancellationToken,
                x => x.Site);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<WarehouseDto>>(items));
        }

        [AllowAnonymous]
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
                Address = createDto.Address,
                SiteId = createDto.SiteId
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
                cancellationToken,
                x => x.Site);
            if (entity == null) return NotFound();

            if (!EnforceSiteRestriction(entity))
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

            if (!EnforceSiteRestriction(entity))
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

            if (!EnforceSiteRestriction(entity))
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

        [AllowAnonymous]
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

        [AllowAnonymous]
        [HttpGet]
        public override async Task<ActionResult<IEnumerable<AssetAssignmentDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<AssetAssignment>();
            var filter = BuildCombinedFilter(siteId, warehouseId);
            var (items, total) = await repo.GetPagedAsync(
                page,
                size,
                search,
                filter,
                null,
                cancellationToken,
                x => x.Asset,
                x => x.AssignedToUser);

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

            if (!EnforceSiteRestriction(entity))
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
                x => x.RequestedByUser,
                x => x.ApprovedByUser);
            if (entity == null) return NotFound();

            if (!EnforceSiteRestriction(entity))
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

        [AllowAnonymous]
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

            if (!EnforceSiteRestriction(entity))
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

        [AllowAnonymous]
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

        [AllowAnonymous]
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
