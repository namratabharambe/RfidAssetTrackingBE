using AutoMapper;
using Domain.Entities;
using Application.DTOs;
using System.Linq;

namespace Application.DTOs
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => src.UserRoles.Select(ur => ur.Role.Name).ToList()))
                .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => src.UserRoles.SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code)).Distinct().ToList()))
                .ForMember(dest => dest.SiteName, opt => opt.MapFrom(src => src.Site != null ? src.Site.Name : null));
            CreateMap<CreateUserDto, User>();

            CreateMap<Role, RoleDto>()
                .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => src.RolePermissions.Select(rp => rp.Permission).ToList()));
            CreateMap<CreateRoleDto, Role>();

            CreateMap<Permission, PermissionDto>();

            CreateMap<Site, SiteDto>();
            CreateMap<CreateSiteDto, Site>();

            CreateMap<Warehouse, WarehouseDto>()
                .ForMember(dest => dest.SiteName, opt => opt.MapFrom(src => src.Site.Name));
            CreateMap<CreateWarehouseDto, Warehouse>();

            CreateMap<Zone, ZoneDto>()
                .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse.Name));
            CreateMap<CreateZoneDto, Zone>();

            CreateMap<Location, LocationDto>()
                .ForMember(dest => dest.ZoneName, opt => opt.MapFrom(src => src.Zone != null ? src.Zone.Name : null))
                .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Zone != null && src.Zone.Warehouse != null ? src.Zone.Warehouse.Name : null));
            CreateMap<CreateLocationDto, Location>();

            CreateMap<Manufacturer, ManufacturerDto>();
            CreateMap<CreateManufacturerDto, Manufacturer>();

            CreateMap<Vendor, VendorDto>();
            CreateMap<CreateVendorDto, Vendor>();

            CreateMap<AssetCategory, AssetCategoryDto>();
            CreateMap<CreateAssetCategoryDto, AssetCategory>();

            CreateMap<Asset, AssetDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.LocationName, opt => opt.MapFrom(src => src.Location != null ? src.Location.Name : null));

            CreateMap<RFIDTag, RFIDTagDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.AssetName, opt => opt.MapFrom(src => src.Asset != null ? src.Asset.Name : null));
            CreateMap<CreateRFIDTagDto, RFIDTag>();

            CreateMap<Barcode, BarcodeDto>()
                .ForMember(dest => dest.AssetName, opt => opt.MapFrom(src => src.Asset != null ? src.Asset.Name : null));
            CreateMap<CreateBarcodeDto, Barcode>();

            CreateMap<GPSDevice, GPSDeviceDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.AssetName, opt => opt.MapFrom(src => src.Asset != null ? src.Asset.Name : null));
            CreateMap<CreateGPSDeviceDto, GPSDevice>();

            CreateMap<GPSHistory, GPSHistoryDto>()
                .ForMember(dest => dest.Imei, opt => opt.MapFrom(src => src.GPSDevice.Imei));

            CreateMap<Reader, ReaderDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.SiteName, opt => opt.MapFrom(src => src.Site.Name));
            CreateMap<CreateReaderDto, Reader>()
                .ForMember(dest => dest.Status, opt => opt.ConvertUsing(new ReaderStatusConverter(), src => src.Status));

            CreateMap<HandheldDevice, HandheldDeviceDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.AssignedUsername, opt => opt.MapFrom(src => src.AssignedUser != null ? src.AssignedUser.Username : null));
            CreateMap<CreateHandheldDeviceDto, HandheldDevice>();

            CreateMap<AssetAssignment, AssetAssignmentDto>()
                .ForMember(dest => dest.AssetName, opt => opt.MapFrom(src => src.Asset.Name))
                .ForMember(dest => dest.AssetNumber, opt => opt.MapFrom(src => src.Asset.AssetNumber))
                .ForMember(dest => dest.AssignedToUsername, opt => opt.MapFrom(src => src.AssignedToUser.Username));
            CreateMap<CreateAssetAssignmentDto, AssetAssignment>();

            CreateMap<AssetTransfer, AssetTransferDto>()
                .ForMember(dest => dest.AssetName, opt => opt.MapFrom(src => src.Asset != null ? src.Asset.Name : src.ItemName))
                .ForMember(dest => dest.AssetNumber, opt => opt.MapFrom(src => src.Asset != null ? src.Asset.AssetNumber : ""))
                .ForMember(dest => dest.ItemName, opt => opt.MapFrom(src => src.ItemName ?? (src.Asset != null ? src.Asset.Name : null)))
                .ForMember(dest => dest.SourceSiteName, opt => opt.MapFrom(src => src.SourceSite != null ? src.SourceSite.Name : ""))
                .ForMember(dest => dest.DestinationSiteName, opt => opt.MapFrom(src => src.DestinationSite != null ? src.DestinationSite.Name : ""))
                .ForMember(dest => dest.SourceLocationName, opt => opt.MapFrom(src => src.SourceLocation != null ? src.SourceLocation.Name : null))
                .ForMember(dest => dest.DestinationLocationName, opt => opt.MapFrom(src => src.DestinationLocation != null ? src.DestinationLocation.Name : null))
                .ForMember(dest => dest.RequestedByUsername, opt => opt.MapFrom(src => src.RequestedByUser != null ? src.RequestedByUser.Username : ""))
                .ForMember(dest => dest.ApprovedByUsername, opt => opt.MapFrom(src => src.ApprovedByUser != null ? src.ApprovedByUser.Username : null))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
            CreateMap<CreateAssetTransferDto, AssetTransfer>();

            CreateMap<AssetIssuance, AssetIssuanceDto>();

            CreateMap<AssetMovement, AssetMovementDto>()
                .ForMember(dest => dest.AssetName, opt => opt.MapFrom(src => src.Asset.Name))
                .ForMember(dest => dest.AssetNumber, opt => opt.MapFrom(src => src.Asset.AssetNumber))
                .ForMember(dest => dest.SourceLocationName, opt => opt.MapFrom(src => src.SourceLocation != null ? src.SourceLocation.Name : null))
                .ForMember(dest => dest.DestinationLocationName, opt => opt.MapFrom(src => src.DestinationLocation != null ? src.DestinationLocation.Name : null))
                .ForMember(dest => dest.ReaderName, opt => opt.MapFrom(src => src.Reader != null ? src.Reader.Name : null))
                .ForMember(dest => dest.HandheldDeviceName, opt => opt.MapFrom(src => src.HandheldDevice != null ? src.HandheldDevice.Name : null));

            CreateMap<InventoryAudit, InventoryAuditDto>()
                .ForMember(dest => dest.AuditorUsername, opt => opt.MapFrom(src => src.AuditorUser.Username))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<InventoryAuditItem, InventoryAuditItemDto>()
                .ForMember(dest => dest.AssetName, opt => opt.MapFrom(src => src.Asset.Name))
                .ForMember(dest => dest.AssetNumber, opt => opt.MapFrom(src => src.Asset.AssetNumber))
                .ForMember(dest => dest.ExpectedLocationName, opt => opt.MapFrom(src => src.ExpectedLocation != null ? src.ExpectedLocation.Name : null))
                .ForMember(dest => dest.ScannedLocationName, opt => opt.MapFrom(src => src.ScannedLocation != null ? src.ScannedLocation.Name : null))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<ScanSession, ScanSessionDto>()
                .ForMember(dest => dest.ReaderName, opt => opt.MapFrom(src => src.Reader != null ? src.Reader.Name : null))
                .ForMember(dest => dest.HandheldDeviceName, opt => opt.MapFrom(src => src.HandheldDevice != null ? src.HandheldDevice.Name : null));

            CreateMap<ScanEvent, ScanEventDto>()
                .ForMember(dest => dest.ReaderName, opt => opt.MapFrom(src => src.Reader != null ? src.Reader.Name : null))
                .ForMember(dest => dest.HandheldDeviceName, opt => opt.MapFrom(src => src.HandheldDevice != null ? src.HandheldDevice.Name : null))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<Alert, AlertDto>()
                .ForMember(dest => dest.AssetName, opt => opt.MapFrom(src => src.Asset != null ? src.Asset.Name : null))
                .ForMember(dest => dest.AlertType, opt => opt.MapFrom(src => src.AlertType.ToString()))
                .ForMember(dest => dest.Severity, opt => opt.MapFrom(src => src.Severity.ToString()))
                .ForMember(dest => dest.ResolvedByUsername, opt => opt.MapFrom(src => src.ResolvedByUser != null ? src.ResolvedByUser.Username : null));

            CreateMap<Notification, NotificationDto>();

            CreateMap<Settings, SettingsDto>();
            CreateMap<CreateSettingsDto, Settings>();
        }
    }

    public class ReaderStatusConverter : AutoMapper.IValueConverter<string?, Domain.Enums.DeviceStatus>
    {
        public Domain.Enums.DeviceStatus Convert(string? sourceMember, AutoMapper.ResolutionContext context)
        {
            if (sourceMember != null && System.Enum.TryParse<Domain.Enums.DeviceStatus>(sourceMember, true, out var result))
                return result;
            return Domain.Enums.DeviceStatus.Online;
        }
    }
}
