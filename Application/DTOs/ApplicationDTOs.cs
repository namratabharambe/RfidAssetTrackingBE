using System;
using System.Collections.Generic;

namespace Application.DTOs
{
    // Security & Auth DTOs
    public record UserDto
    {
        public Guid Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public Guid? SiteId { get; init; }
        public string? SiteName { get; init; }
        public List<string> Roles { get; init; } = new();
        public List<string> Permissions { get; init; } = new();
        public List<SiteDto> AllowedSites { get; init; } = new();
        public List<WarehouseDto> AllowedWarehouses { get; init; } = new();
        public List<Guid> SelectedSiteIds { get; init; } = new();
        public List<Guid> SelectedWarehouseIds { get; init; } = new();
        public string Status => IsActive ? "Active" : "Inactive";

        public UserDto() { }

        public UserDto(Guid id, string username, string email, bool isActive, Guid? siteId, string? siteName, List<string> roles, List<string> permissions, List<SiteDto>? allowedSites = null, List<WarehouseDto>? allowedWarehouses = null, List<Guid>? selectedSiteIds = null, List<Guid>? selectedWarehouseIds = null)
        {
            Id = id; Username = username; Email = email; IsActive = isActive;
            SiteId = siteId; SiteName = siteName; Roles = roles; Permissions = permissions;
            AllowedSites = allowedSites ?? new();
            AllowedWarehouses = allowedWarehouses ?? new();
            SelectedSiteIds = selectedSiteIds ?? new();
            SelectedWarehouseIds = selectedWarehouseIds ?? new();
        }
    }
    public record CreateUserDto(
        string Username, 
        string Email, 
        string Password, 
        List<Guid>? RoleIds = null, 
        Guid? SiteId = null, 
        List<string>? Roles = null, 
        string? Role = null, 
        List<Guid>? AllowedSiteIds = null, 
        List<Guid>? AllowedWarehouseIds = null,
        List<Guid>? SelectedSiteIds = null,
        List<Guid>? SelectedWarehouseIds = null,
        List<Guid>? SiteIds = null,
        List<Guid>? WarehouseIds = null);

    public record UpdateUserDto(
        string Username, 
        string Email, 
        bool IsActive, 
        List<Guid>? RoleIds = null, 
        Guid? SiteId = null, 
        List<string>? Roles = null, 
        string? Role = null, 
        List<Guid>? AllowedSiteIds = null, 
        List<Guid>? AllowedWarehouseIds = null,
        List<Guid>? SelectedSiteIds = null,
        List<Guid>? SelectedWarehouseIds = null,
        List<Guid>? SiteIds = null,
        List<Guid>? WarehouseIds = null);
    public record LoginDto(string? Username, string Password, string? Email = null);
    public record LoginResponseDto(string Token, string RefreshToken, UserDto User);
    public record SwitchContextDto(Guid? SiteId = null, Guid? WarehouseId = null);
    public record RefreshTokenDto(string Token, string RefreshToken);
    public record ForgotPasswordDto(string Email);
    public record ResetPasswordDto(string Token, string NewPassword);
    public record ChangePasswordDto(string CurrentPassword, string NewPassword);

    public record RoleDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public List<PermissionDto> Permissions { get; init; } = new();

        public RoleDto() { }

        public RoleDto(Guid id, string name, string description, List<PermissionDto>? permissions = null)
        {
            Id = id; Name = name; Description = description;
            Permissions = permissions ?? new();
        }
    }
    public record CreateRoleDto(string Name, string Description, List<Guid> PermissionIds);

    public record PermissionDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;

        public PermissionDto() { }

        public PermissionDto(Guid id, string name, string code)
        {
            Id = id; Name = name; Code = code;
        }
    }

    // Physical Structure DTOs
    public record SiteDto(Guid Id, string Code, string Name, string? Address);
    public record CreateSiteDto(string Code, string Name, string? Address);

    public record WarehouseDto(Guid Id, string Code, string Name, string? Address, Guid SiteId, string SiteName);
    public record CreateWarehouseDto(string Code, string Name, string? Address, Guid SiteId);

    public record ZoneDto(Guid Id, string Code, string Name, string? Description, Guid WarehouseId, string WarehouseName);
    public record CreateZoneDto(string Code, string Name, string? Description, Guid WarehouseId);

    public record LocationDto
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public Guid ZoneId { get; init; }
        public string ZoneName { get; init; } = string.Empty;
        public string? WarehouseName { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
    }
    public record CreateLocationDto(string Code, string Name, Guid ZoneId, decimal? Latitude, decimal? Longitude);

    // Asset Traceability DTOs
    public record ManufacturerDto(Guid Id, string Name, string? ContactInfo, string? SupportEmail, string? SupportPhone);
    public record CreateManufacturerDto(string Name, string? ContactInfo, string? SupportEmail, string? SupportPhone);

    public record VendorDto(Guid Id, string Name, string? ContactName, string? Email, string? Phone, string? Address);
    public record CreateVendorDto(string Name, string? ContactName, string? Email, string? Phone, string? Address);

    public record AssetCategoryDto(Guid Id, string Name, string? Description);
    public record CreateAssetCategoryDto(string Name, string? Description);

    // Tags & GPS DTOs
    public record RFIDTagDto(Guid Id, string EpcCode, string? TidCode, string Status, Guid? AssetId, string? AssetName);
    public record CreateRFIDTagDto(string EpcCode, string? TidCode, Guid? AssetId);

    public record BarcodeDto(Guid Id, string BarcodeValue, string Format, bool IsActive, Guid? AssetId, string? AssetName);
    public record CreateBarcodeDto(string BarcodeValue, string Format, Guid? AssetId);

    public record GPSDeviceDto(Guid Id, string Imei, string? SimNumber, int BatteryLevel, string Status, Guid? AssetId, string? AssetName);
    public record CreateGPSDeviceDto(string Imei, string? SimNumber, Guid? AssetId);

    public record GPSHistoryDto(Guid Id, Guid GPSDeviceId, string Imei, double Latitude, double Longitude, double Speed, double Heading, DateTime Timestamp, string? GeofenceStatus);
    public record PostGPSLocationDto(string Imei, double Latitude, double Longitude, double Speed, double Heading, DateTime Timestamp);

    // Hardware DTOs
    public record ReaderDto(Guid Id, string Name, string IpAddress, int Port, string Status, int AntennaCount, int PowerDbm, string? Model, Guid SiteId, string SiteName);
    public record CreateReaderDto(string Name, string IpAddress, int Port, int AntennaCount, int PowerDbm, Guid SiteId, string? Model = null, string? Status = null);

    public record HandheldDeviceDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = null!;
        public string DeviceSerial { get; init; } = null!;
        public string? Model { get; init; }
        public string Status { get; init; } = null!;
        public Guid? AssignedUserId { get; init; }
        public string? AssignedUsername { get; init; }
    }
    public record CreateHandheldDeviceDto(string Name, string DeviceSerial, string? Model, Guid? AssignedUserId);

    // Operations DTOs
    public record AssetAssignmentDto
    {
        public Guid Id { get; init; }
        public Guid AssetId { get; init; }
        public string AssetName { get; init; } = null!;
        public string AssetNumber { get; init; } = null!;
        public Guid AssignedToUserId { get; init; }
        public string AssignedToUsername { get; init; } = null!;
        public string? CustodianName { get; init; }
        public DateTime AssignedDate { get; init; }
        public DateTime? ExpectedReturnDate { get; init; }
        public DateTime? ActualReturnDate { get; init; }
        public string? Purpose { get; init; }
        public string Status { get; init; } = null!;
        public string? Notes { get; init; }
    }
    public record CreateAssetAssignmentDto(Guid AssetId, Guid AssignedToUserId, string? CustodianName, DateTime? ExpectedReturnDate, string? Purpose, string? Notes);

    public record AssetTransferDto(
        Guid Id,
        Guid AssetId,
        string AssetName,
        string AssetNumber,
        string? ItemName,
        Guid SourceSiteId,
        string SourceSiteName,
        Guid DestinationSiteId,
        string DestinationSiteName,
        Guid? SourceLocationId,
        string? SourceLocationName,
        Guid? DestinationLocationId,
        string? DestinationLocationName,
        decimal Quantity,
        string? Unit,
        string? Image,
        string? DeliveryChallanNo,
        string? InvoiceNumber,
        DateTime TransferDate,
        string Status,
        Guid RequestedByUserId,
        string RequestedByUsername,
        Guid? ApprovedByUserId,
        string? ApprovedByUsername,
        string? Remarks
    );

    public record CreateAssetTransferDto(
        Guid AssetId,
        string? ItemName,
        Guid DestinationSiteId,
        Guid? SourceLocationId = null,
        Guid? DestinationLocationId = null,
        decimal Quantity = 1,
        string? Unit = null,
        string? Image = null,
        string? DeliveryChallanNo = null,
        string? InvoiceNumber = null,
        string? Remarks = null
    );

    public record CentralToSiteTransferDto(
        Guid fromWarehouseId,
        Guid toSiteId,
        string assetCode,
        string assetName,
        decimal quantity,
        string? unit = null,
        string? deliveryChallanNo = null,
        string? transferPhoto = null
    );

    public record SurplusReturnTransferDto(
        Guid fromSiteId,
        Guid toWarehouseId,
        string assetCode,
        string assetName,
        decimal quantity,
        string? unit = null
    );

    public record SiteToSiteTransferDto(
        Guid fromSiteId,
        Guid toSiteId,
        string assetCode,
        string assetName,
        decimal quantity
    );

    public record IssueMaterialRequestDto(
        Guid AssetId,
        string? AssetNumber,
        string? Name,
        string IssuedToPerson,
        string Contractor,
        decimal IssueQuantity,
        string? Unit,
        string Purpose,
        Guid SiteId,
        string? SiteName,
        DateTime? IssuedDate,
        string? Remarks
    );

    public record AssetIssuanceDto
    {
        public Guid Id { get; init; }
        public string IssueCode { get; init; } = string.Empty;
        public Guid AssetId { get; init; }
        public string AssetNumber { get; init; } = string.Empty;
        public string AssetName { get; init; } = string.Empty;
        public string IssuedToPerson { get; init; } = string.Empty;
        public string Contractor { get; init; } = string.Empty;
        public decimal IssueQuantity { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public Guid SiteId { get; init; }
        public string SiteName { get; init; } = string.Empty;
        public DateTime IssuedDate { get; init; }
        public decimal PreviousIssuedQty { get; init; }
        public decimal NewIssuedQty { get; init; }
        public decimal PreviousBalanceQty { get; init; }
        public decimal NewBalanceQty { get; init; }
        public string? Remarks { get; init; }

        public AssetIssuanceDto() { }
    }

    public record AssetMovementDto
    {
        public Guid Id { get; set; }
        public Guid AssetId { get; set; }
        public string AssetName { get; set; } = null!;
        public string AssetNumber { get; set; } = null!;
        public Guid? SourceLocationId { get; set; }
        public string? SourceLocationName { get; set; }
        public Guid? DestinationLocationId { get; set; }
        public string? DestinationLocationName { get; set; }
        public DateTime MovementDate { get; set; }
        public string MovementType { get; set; } = null!;
        public Guid? ReaderId { get; set; }
        public string? ReaderName { get; set; }
        public Guid? HandheldDeviceId { get; set; }
        public string? HandheldDeviceName { get; set; }
        public string? Remarks { get; set; }
    }
    public record CreateAssetMovementDto(Guid AssetId, Guid? DestinationLocationId, string MovementType, Guid? ReaderId, Guid? HandheldDeviceId, string? Remarks);

    // Audit DTOs
    public record InventoryAuditDto
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = null!;
        public DateTime AuditDate { get; init; }
        public string Status { get; init; } = null!;
        public Guid AuditorUserId { get; init; }
        public string? AuditorUsername { get; init; }
        public List<InventoryAuditItemDto> AuditItems { get; init; } = new();
    }
    public record CreateInventoryAuditDto(string Title, Guid AuditorUserId, List<Guid> AssetIds);
    public record InventoryAuditItemDto
    {
        public Guid Id { get; init; }
        public Guid InventoryAuditId { get; init; }
        public Guid AssetId { get; init; }
        public string AssetName { get; init; } = null!;
        public string AssetNumber { get; init; } = null!;
        public Guid? ExpectedLocationId { get; init; }
        public string? ExpectedLocationName { get; init; }
        public Guid? ScannedLocationId { get; init; }
        public string? ScannedLocationName { get; init; }
        public string Status { get; init; } = null!;
        public DateTime? ScannedDate { get; init; }
        public string? Notes { get; init; }
    }

    // Scanning DTOs
    public record ScanSessionDto(Guid Id, string SessionName, DateTime StartTime, DateTime? EndTime, Guid? ReaderId, string? ReaderName, Guid? HandheldDeviceId, string? HandheldDeviceName, bool IsRunning, List<ScanEventDto> ScanEvents);
    public record CreateScanSessionDto(string SessionName, Guid? ReaderId, Guid? HandheldDeviceId);
    public record ScanEventDto(Guid Id, Guid ScanSessionId, string EpcCode, string? TidCode, DateTime Timestamp, int Rssi, int AntennaIndex, Guid? ReaderId, string? ReaderName, Guid? HandheldDeviceId, string? HandheldDeviceName, string Status);
    public record UploadScanDto(string EpcCode, string? TidCode, DateTime Timestamp, int Rssi, int AntennaIndex, Guid? ReaderId, Guid? HandheldDeviceId);

    // Notification & Alert DTOs
    public record AlertDto
    {
        public Guid Id { get; init; }
        public Guid? AssetId { get; init; }
        public string? AssetName { get; init; }
        public string AlertType { get; init; } = null!;
        public string Severity { get; init; } = null!;
        public string Title { get; init; } = null!;
        public string Message { get; init; } = null!;
        public bool IsResolved { get; init; }
        public DateTime? ResolvedDate { get; init; }
        public string? ResolvedByUsername { get; init; }
    }
    public record NotificationDto(Guid Id, string Title, string Message, string Type, bool IsRead, DateTime CreatedDate);

    // Settings DTOs
    public record SettingsDto(Guid Id, string Key, string Value, string? Description, string Group);
    public record CreateSettingsDto(string Key, string Value, string? Description, string Group);

    // Dashboard DTOs
    public record DashboardDto(
        int TotalAssets,
        int AvailableAssets,
        int AssignedAssets,
        int LostAssets,
        int MissingAssets,
        List<SiteStatDto> SiteStats,
        List<ReaderStatusDto> ReaderStatuses,
        List<GPSDeviceStatusDto> GPSStatuses,
        List<ActivityLogDto> RecentActivity,
        List<AlertDto> ActiveAlerts,
        List<ScanCountDto> TodayScans,
        List<ScanCountDto> WeeklyScans,
        List<ScanCountDto> MonthlyScans
    );
    public record SiteStatDto(string SiteName, int Total, int InUse, int Available, int Maintenance, int RfidReadsToday, int GpsPingsToday, int ExceptionAlerts, int ComplianceTasks);
    public record ReaderStatusDto(string Name, string Location, string Status);
    public record GPSDeviceStatusDto(string Name, string AssetName, int Battery, string Status);
    public record ActivityLogDto(string Description, DateTime Timestamp, string Operator);
    public record ScanCountDto(string Label, int Count);
}
