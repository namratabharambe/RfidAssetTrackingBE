using AutoMapper;
using Application.DTOs;
using Application.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
                var claim = User.FindFirst("siteId")?.Value;
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        protected Expression<Func<TEntity, bool>>? GetSiteFilterExpression()
        {
            var siteId = CurrentUserSiteId;
            if (!siteId.HasValue) return null;

            var property = typeof(TEntity).GetProperty("SiteId");
            if (property == null) return null;

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            Expression left = Expression.Property(parameter, property);
            Expression right;

            if (property.PropertyType == typeof(Guid?))
            {
                right = Expression.Constant(siteId, typeof(Guid?));
            }
            else
            {
                right = Expression.Constant(siteId.Value, typeof(Guid));
            }

            var body = Expression.Equal(left, right);
            return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
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
            [FromQuery] int size = 10,
            [FromQuery] string? search = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<TEntity>();
            var filter = GetSiteFilterExpression();
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
    }

    [Authorize]
    [ApiController]
    [Route("api/warehouses")]
    public class WarehousesController : CrudControllerBase<Warehouse, WarehouseDto, CreateWarehouseDto>
    {
        public WarehousesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }

    [Authorize]
    [ApiController]
    [Route("api/zones")]
    public class ZonesController : CrudControllerBase<Zone, ZoneDto, CreateZoneDto>
    {
        public ZonesController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }

    [Authorize]
    [ApiController]
    [Route("api/locations")]
    public class LocationsController : CrudControllerBase<Location, LocationDto, CreateLocationDto>
    {
        public LocationsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
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
    }

    [Authorize]
    [ApiController]
    [Route("api/transfers")]
    public class AssetTransfersController : CrudControllerBase<AssetTransfer, AssetTransferDto, CreateAssetTransferDto>
    {
        public AssetTransfersController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }

    [Authorize]
    [ApiController]
    [Route("api/movements")]
    public class AssetMovementsController : CrudControllerBase<AssetMovement, AssetMovementDto, CreateAssetMovementDto>
    {
        public AssetMovementsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
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
    }

    [Authorize]
    [ApiController]
    [Route("api/settings")]
    public class SettingsController : CrudControllerBase<Settings, SettingsDto, CreateSettingsDto>
    {
        public SettingsController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper) { }
    }
}
