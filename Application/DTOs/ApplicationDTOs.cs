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
        public string Status => IsActive ? "Active" : "Inactive";

        public UserDto() { }

        public UserDto(Guid id, string username, string email, bool isActive, Guid? siteId, string? siteName, List<string> roles, List<string> permissions)
        {
            Id = id; Username = username; Email = email; IsActive = isActive;
            SiteId = siteId; SiteName = siteName; Roles = roles; Permissions = permissions;
        }
    }
    public record CreateUserDto(string Username, string Email, string Password, List<Guid> RoleIds, Guid? SiteId = null);
    public record UpdateUserDto(string Username, string Email, bool IsActive, List<Guid> RoleIds, Guid? SiteId = null);
    public record LoginDto(string Username, string Password);
    public record LoginResponseDto(string Token, string RefreshToken, UserDto User);
    public record RefreshTokenDto(string Token, string RefreshToken);
    public record ForgotPasswordDto(string Email);
    public record ResetPasswordDto(string Token, string NewPassword);
    public record ChangePasswordDto(string CurrentPassword, string NewPassword);

    public record RoleDto(Guid Id, string Name, string Description, List<PermissionDto> Permissions);
    public record CreateRoleDto(string Name, string Description, List<Guid> PermissionIds);

    public record PermissionDto(Guid Id, string Name, string Code);

    // Physical Structure DTOs
    public record SiteDto(Guid Id, string Code, string Name, string? Address);
    public record CreateSiteDto(string Code, string Name, string? Address);

    public record WarehouseDto(Guid Id, string Code, string Name, string? Address, Guid SiteId, string SiteName);
    public record CreateWarehouseDto(string Code, string Name, string? Address, Guid SiteId);

    public record ZoneDto(Guid Id, string Code, string Name, string? Description, Guid WarehouseId, string WarehouseName);
    public record CreateZoneDto(string Code, string Name, string? Description, Guid WarehouseId);

    public record LocationDto(Guid Id, string Code, string Name, Guid ZoneId, string ZoneName, decimal? Latitude, decimal? Longitude);
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

    public record HandheldDeviceDto(Guid Id, string Name, string DeviceSerial, string? Model, string Status, Guid? AssignedUserId, string? AssignedUsername);
    public record CreateHandheldDeviceDto(string Name, string DeviceSerial, string? Model, Guid? AssignedUserId);

    // Operations DTOs
    public record AssetAssignmentDto(Guid Id, Guid AssetId, string AssetName, string AssetNumber, Guid AssignedToUserId, string AssignedToUsername, string? CustodianName, DateTime AssignedDate, DateTime? ExpectedReturnDate, DateTime? ActualReturnDate, string? Purpose, string Status, string? Notes);
    public record CreateAssetAssignmentDto(Guid AssetId, Guid AssignedToUserId, string? CustodianName, DateTime? ExpectedReturnDate, string? Purpose, string? Notes);

    public record AssetTransferDto(Guid Id, Guid AssetId, string AssetName, string AssetNumber, Guid SourceSiteId, string SourceSiteName, Guid DestinationSiteId, string DestinationSiteName, DateTime TransferDate, string Status, Guid RequestedByUserId, string RequestedByUsername, Guid? ApprovedByUserId, string? ApprovedByUsername, string? Remarks);
    public record CreateAssetTransferDto(Guid AssetId, Guid DestinationSiteId, string? Remarks);

    public record AssetMovementDto(Guid Id, Guid AssetId, string AssetName, string AssetNumber, Guid? SourceLocationId, string? SourceLocationName, Guid? DestinationLocationId, string? DestinationLocationName, DateTime MovementDate, string MovementType, Guid? ReaderId, string? ReaderName, Guid? HandheldDeviceId, string? HandheldDeviceName, string? Remarks);
    public record CreateAssetMovementDto(Guid AssetId, Guid? DestinationLocationId, string MovementType, Guid? ReaderId, Guid? HandheldDeviceId, string? Remarks);

    // Audit DTOs
    public record InventoryAuditDto(Guid Id, string Title, DateTime AuditDate, string Status, Guid AuditorUserId, string AuditorUsername, List<InventoryAuditItemDto> AuditItems);
    public record CreateInventoryAuditDto(string Title, Guid AuditorUserId, List<Guid> AssetIds);
    public record InventoryAuditItemDto(Guid Id, Guid InventoryAuditId, Guid AssetId, string AssetName, string AssetNumber, Guid? ExpectedLocationId, string? ExpectedLocationName, Guid? ScannedLocationId, string? ScannedLocationName, string Status, DateTime? ScannedDate, string? Notes);

    // Scanning DTOs
    public record ScanSessionDto(Guid Id, string SessionName, DateTime StartTime, DateTime? EndTime, Guid? ReaderId, string? ReaderName, Guid? HandheldDeviceId, string? HandheldDeviceName, bool IsRunning, List<ScanEventDto> ScanEvents);
    public record CreateScanSessionDto(string SessionName, Guid? ReaderId, Guid? HandheldDeviceId);
    public record ScanEventDto(Guid Id, Guid ScanSessionId, string EpcCode, string? TidCode, DateTime Timestamp, int Rssi, int AntennaIndex, Guid? ReaderId, string? ReaderName, Guid? HandheldDeviceId, string? HandheldDeviceName, string Status);
    public record UploadScanDto(string EpcCode, string? TidCode, DateTime Timestamp, int Rssi, int AntennaIndex, Guid? ReaderId, Guid? HandheldDeviceId);

    // Notification & Alert DTOs
    public record AlertDto(Guid Id, Guid? AssetId, string? AssetName, string AlertType, string Severity, string Title, string Message, bool IsResolved, DateTime? ResolvedDate, string? ResolvedByUsername);
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
    public record SiteStatDto(string SiteName, int Total, int InUse, int Available, int Maintenance);
    public record ReaderStatusDto(string Name, string Location, string Status);
    public record GPSDeviceStatusDto(string Name, string AssetName, int Battery, string Status);
    public record ActivityLogDto(string Description, DateTime Timestamp, string Operator);
    public record ScanCountDto(string Label, int Count);
}
