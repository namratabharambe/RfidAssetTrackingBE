using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GpsController : ControllerBase
    {
        private readonly AssetTrackingDbContext _db;
        private readonly MediatR.IMediator _mediator;
        private static string _ezzlocToken;
        private static DateTime _tokenExpiry;

        private const string GpsApiUrl = "https://www.ezzloc.net/gpsapi";

        public GpsController(AssetTrackingDbContext db, MediatR.IMediator mediator)
        {
            _db = db;
            _mediator = mediator;
        }

        [HttpGet("getTrackTablePageList")]
        public async Task<IActionResult> GetVehicleLocation(
            [FromQuery] string vehicleId,
            [FromQuery] DateTime beginGPSTime,
            [FromQuery] DateTime endGPSTime)
        {
            var gpsDevice = await _db.GPSDevices.FirstOrDefaultAsync(g => g.Imei == vehicleId);
            if (gpsDevice != null)
            {
                var localHistory = await _db.GPSHistories
                    .Where(h => h.GPSDeviceId == gpsDevice.Id && h.Timestamp >= beginGPSTime && h.Timestamp <= endGPSTime)
                    .OrderBy(h => h.Timestamp)
                    .ToListAsync();

                if (localHistory != null && localHistory.Any())
                {
                    var dataList = localHistory.Select(h => new
                    {
                        VehicleID = gpsDevice.Imei,
                        RegName = gpsDevice.Imei,
                        DeviceNum = gpsDevice.Imei,
                        GpsTime = new DateTimeOffset(h.Timestamp).ToUnixTimeMilliseconds(),
                        Lat = h.Latitude.ToString("F6"),
                        Lon = h.Longitude.ToString("F6"),
                        Speed = h.Speed.ToString("F0"),
                        Direction = h.Heading.ToString("F0"),
                        Battery = gpsDevice.BatteryLevel.ToString(),
                        UpdateTime = new DateTimeOffset(h.CreatedOn).ToUnixTimeMilliseconds()
                    }).ToList();

                    return Ok(new
                    {
                        cmd = "getTrackData",
                        result = 1,
                        resultNote = "Success",
                        detail = dataList
                    });
                }
            }

            try
            {
                var token = await GetEzzlocTokenAsync();

                var vehicleListJson = await PostGpsApiAsync(new
                {
                    cmd = "getVehicleList",
                    token = token,
                    language = 2,
                    @params = new { }
                });

                using var vehicleListDoc = JsonDocument.Parse(vehicleListJson);
                var root = vehicleListDoc.RootElement;

                if (root.TryGetProperty("result", out var resultProp) && resultProp.GetInt32() == 1 && root.TryGetProperty("detail", out var detailProp))
                {
                    string ezzlocVehicleId = null;

                    if (detailProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var vehicle in detailProp.EnumerateArray())
                        {
                            if (vehicle.TryGetProperty("DeviceNum", out var deviceNum) && deviceNum.GetString() == vehicleId)
                            {
                                if (vehicle.TryGetProperty("VehicleID", out var vid)) ezzlocVehicleId = vid.ToString();
                                break;
                            }
                        }
                    }
                    else if (detailProp.ValueKind == JsonValueKind.Object && detailProp.TryGetProperty("list", out var listProp) && listProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var vehicle in listProp.EnumerateArray())
                        {
                            if (vehicle.TryGetProperty("DeviceNum", out var deviceNum) && deviceNum.GetString() == vehicleId)
                            {
                                if (vehicle.TryGetProperty("VehicleID", out var vid)) ezzlocVehicleId = vid.ToString();
                                break;
                            }
                        }
                    }

                    if (ezzlocVehicleId != null)
                    {
                        long beginTimestamp = new DateTimeOffset(beginGPSTime).ToUnixTimeMilliseconds();
                        long endTimestamp = new DateTimeOffset(endGPSTime).ToUnixTimeMilliseconds();

                        var locationJson = await PostGpsApiAsync(new
                        {
                            cmd = "getTrackData",
                            token = token,
                            language = 2,
                            @params = new
                            {
                                VehicleID = ezzlocVehicleId,
                                BeginTime = beginTimestamp,
                                EndTime = endTimestamp
                            }
                        });

                        using var locDoc = JsonDocument.Parse(locationJson);
                        if (locDoc.RootElement.TryGetProperty("detail", out var trackDetail))
                        {
                            return Ok(locDoc.RootElement);
                        }
                    }
                }
            }
            catch
            {
                // External cloud API call failed or device not on cloud
            }

            // Return empty route history if no real GPS movement pings exist for this date
            return Ok(new
            {
                cmd = "getTrackData",
                result = 1,
                resultNote = "No recorded GPS route history for this date",
                detail = new System.Collections.Generic.List<object>()
            });
        }

        [HttpGet("vehicle-location/{vehicleId}")]
        public async Task<IActionResult> GetVehicleLocation(string vehicleId)
        {
            var token = await GetEzzlocTokenAsync();

            var vehicleListJson = await PostGpsApiAsync(new
            {
                cmd = "getVehicleList",
                token = token,
                language = 2,
                @params = new { }
            });

            using var vehicleListDoc = JsonDocument.Parse(vehicleListJson);
            var root = vehicleListDoc.RootElement;

            if (!root.TryGetProperty("result", out var resultProp) || resultProp.GetInt32() != 1)
                return BadRequest($"getVehicleList failed: {vehicleListJson}");

            if (!root.TryGetProperty("detail", out var detailProp))
                return BadRequest($"No detail in getVehicleList response: {vehicleListJson}");

            string ezzlocVehicleId = null;

            if (detailProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var vehicle in detailProp.EnumerateArray())
                {
                    if (vehicle.TryGetProperty("DeviceNum", out var deviceNum) &&
                        deviceNum.GetString() == vehicleId)
                    {
                        if (vehicle.TryGetProperty("VehicleID", out var vid))
                        {
                            ezzlocVehicleId = vid.ToString();
                        }
                        break;
                    }
                }
            }
            else if (detailProp.ValueKind == JsonValueKind.Object &&
                     detailProp.TryGetProperty("list", out var listProp) &&
                     listProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var vehicle in listProp.EnumerateArray())
                {
                    if (vehicle.TryGetProperty("DeviceNum", out var deviceNum) &&
                        deviceNum.GetString() == vehicleId)
                    {
                        if (vehicle.TryGetProperty("VehicleID", out var vid))
                        {
                            ezzlocVehicleId = vid.ToString();
                        }
                        break;
                    }
                }
            }

            if (ezzlocVehicleId == null)
                return NotFound($"No vehicle found with DeviceNum = '{vehicleId}'");

            var locationJson = await PostGpsApiAsync(new
            {
                cmd = "getVehiclesLocation",
                token = token,
                language = 2,
                @params = new
                {
                    VehicleIDs = ezzlocVehicleId,
                    LastTime = "0"
                }
            });

            return Ok(JsonDocument.Parse(locationJson).RootElement);
        }

        private async Task<string> PostGpsApiAsync(object body)
        {
            using var httpClient = new HttpClient();
            var json = JsonSerializer.Serialize(body);
            var request = new HttpRequestMessage(HttpMethod.Post, GpsApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            var response = await httpClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }

        private async Task<string> GetEzzlocTokenAsync()
        {
            if (!string.IsNullOrEmpty(_ezzlocToken) && _tokenExpiry > DateTime.UtcNow)
                return _ezzlocToken;

            var loginBody = new
            {
                cmd = "login",
                token = "",
                language = 2,
                @params = new
                {
                    UserCode = "Prosper66",
                    Password = "e10adc3949ba59abbe56e057f20f883e"
                }
            };

            var rawResponse = await PostGpsApiAsync(loginBody);

            var result = JsonSerializer.Deserialize<LoginResponse>(
                rawResponse,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null || result.Result != 1)
                throw new Exception($"Login failed: {rawResponse}");

            _ezzlocToken = result.Detail?.Token
                ?? throw new Exception($"No token in login response: {rawResponse}");

            _tokenExpiry = DateTime.UtcNow.AddHours(11);

            return _ezzlocToken;
        }

        public class LoginResponse
        {
            public string Cmd { get; set; }
            public int Result { get; set; }
            public string ResultNote { get; set; }
            public LoginDetail Detail { get; set; }
        }

        public class LoginDetail
        {
            public int Result { get; set; }
            public string Token { get; set; }
            public string UserCode { get; set; }
            public bool IsHaveBattyDel { get; set; }
            public string PostUrl { get; set; }
        }

        [AllowAnonymous]
        [HttpPost("receive")]
        public async Task<IActionResult> ReceiveGps(
            [FromQuery] string id,
            [FromQuery] long timestamp,
            [FromQuery] double lat,
            [FromQuery] double lon,
            [FromQuery] double speed = 0,
            [FromQuery] double bearing = 0,
            [FromQuery] double batt = 0,
            [FromQuery] bool charge = false,
            [FromQuery] bool mock = false
        )
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("id parameter is required");
            }
            try
            {
                DateTime gpsDateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                
                // 1. Update Vehicles table
                var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.DeviceNum == id);
                if (vehicle == null)
                {
                    vehicle = new Vehicle
                    {
                        Id = Guid.NewGuid(),
                        DeviceNum = id,
                        RegName = id,
                        Status = "Online",
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.Vehicles.Add(vehicle);
                }
                vehicle.Lat = lat;
                vehicle.Lon = lon;
                vehicle.Speed = speed;
                vehicle.Direction = bearing;
                vehicle.Battery = batt;
                vehicle.GpsTime = gpsDateTime;
                vehicle.UpdateTime = DateTime.UtcNow;
                vehicle.Status = "ACC ON,LBS";
                
                var gpsDevice = await _db.GPSDevices.FirstOrDefaultAsync(g => g.Imei == id);
                if (gpsDevice == null)
                {
                    gpsDevice = new Domain.Entities.GPSDevice
                    {
                        Id = Guid.NewGuid(),
                        Imei = id,
                        SimNumber = "Sim " + id.Substring(Math.Max(0, id.Length - 4)),
                        BatteryLevel = batt > 0 ? (int)batt : 100,
                        Status = Domain.Enums.DeviceStatus.Online,
                        CreatedOn = DateTime.UtcNow
                    };
                    _db.GPSDevices.Add(gpsDevice);
                }
                else
                {
                    gpsDevice.Status = Domain.Enums.DeviceStatus.Online;
                    gpsDevice.BatteryLevel = batt > 0 ? (int)batt : 100;
                    gpsDevice.UpdatedOn = DateTime.UtcNow;
                    _db.GPSDevices.Update(gpsDevice);
                }

                var history = new Domain.Entities.GPSHistory
                {
                    Id = Guid.NewGuid(),
                    GPSDeviceId = gpsDevice.Id,
                    Latitude = lat,
                    Longitude = lon,
                    Speed = speed,
                    Heading = bearing,
                    Timestamp = gpsDateTime,
                    CreatedOn = DateTime.UtcNow
                };
                _db.GPSHistories.Add(history);

                await _db.SaveChangesAsync();
                return Content("success");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"error: {ex.Message}");
            }
        }

        [HttpGet("vehicle-android-location/{deviceNum}")]
        public async Task<IActionResult> GetVehicleAndroidLocation(string deviceNum)
        {
            var result = await _mediator.Send(new Application.GPS.Queries.GetGpsAndroidLocation.GetGpsAndroidLocationQuery(deviceNum));
            return Ok(result);
        }

        [HttpGet("vehicles")]
        public async Task<IActionResult> GetVehicles(
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null)
        {
            // Resolve warehouse ID from query, header, or claims
            Guid? targetWhId = warehouseId;
            if (!targetWhId.HasValue && Request.Headers.TryGetValue("X-Warehouse-Id", out var hWh) && Guid.TryParse(hWh.FirstOrDefault(), out var parsedHWh) && parsedHWh != Guid.Empty)
            {
                targetWhId = parsedHWh;
            }

            // Resolve site ID from query, header, or claims
            Guid? targetSiteId = siteId;
            if (!targetSiteId.HasValue && Request.Headers.TryGetValue("X-Site-Id", out var hSite) && Guid.TryParse(hSite.FirstOrDefault(), out var parsedHSite) && parsedHSite != Guid.Empty)
            {
                targetSiteId = parsedHSite;
            }

            var isSuperAdmin = User.IsInRole("Super Admin") 
                            || User.IsInRole("System Administrator")
                            || User.HasClaim(c => (c.Type == "allowed_site_ids" || c.Type == "sites") && (c.Value == "ALL" || c.Value == "GLOBAL_ALL_SITES"));

            var allowedSiteGuids = User.Claims
                .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue && g.Value != Guid.Empty)
                .Select(g => g!.Value)
                .Distinct()
                .ToHashSet();

            var allowedWhGuids = User.Claims
                .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                .Where(g => g.HasValue && g.Value != Guid.Empty)
                .Select(g => g!.Value)
                .Distinct()
                .ToHashSet();

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;
            var currentEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                            ?? User.FindFirst("email")?.Value
                            ?? User.Identity?.Name;

            var gpsQuery = _db.GPSDevices.Include(g => g.Asset).Where(g => !g.IsDeleted);

            if (targetWhId.HasValue)
            {
                gpsQuery = gpsQuery.Where(g => g.Asset != null && g.Asset.WarehouseId == targetWhId.Value);
            }
            else if (targetSiteId.HasValue)
            {
                gpsQuery = gpsQuery.Where(g => g.Asset != null && g.Asset.SiteId == targetSiteId.Value);
            }
            else if (!isSuperAdmin)
            {
                if (allowedWhGuids.Any())
                {
                    gpsQuery = gpsQuery.Where(g => 
                        (g.Asset != null && g.Asset.WarehouseId.HasValue && allowedWhGuids.Contains(g.Asset.WarehouseId.Value)) ||
                        (g.AssetId == null && (g.CreatedBy == currentUserId || (currentEmail != null && g.CreatedBy == currentEmail)))
                    );
                }
                else if (allowedSiteGuids.Any())
                {
                    gpsQuery = gpsQuery.Where(g => 
                        (g.Asset != null && g.Asset.SiteId.HasValue && allowedSiteGuids.Contains(g.Asset.SiteId.Value)) ||
                        (g.AssetId == null && (g.CreatedBy == currentUserId || (currentEmail != null && g.CreatedBy == currentEmail)))
                    );
                }
                else
                {
                    gpsQuery = gpsQuery.Where(g => 
                        (g.Asset != null && (g.Asset.CreatedBy == currentUserId || (currentEmail != null && g.Asset.CreatedBy == currentEmail))) ||
                        (g.CreatedBy == currentUserId || (currentEmail != null && g.CreatedBy == currentEmail))
                    );
                }
            }

            var allowedGpsDevices = await gpsQuery.ToListAsync();
            var allowedImeis = allowedGpsDevices.Select(g => g.Imei).Where(i => !string.IsNullOrEmpty(i)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var localVehicles = await _db.Vehicles
                .Where(v => !v.IsDeleted && (isSuperAdmin && !targetSiteId.HasValue && !targetWhId.HasValue ? true : allowedImeis.Contains(v.DeviceNum)))
                .ToListAsync();

            bool changed = false;
            var rand = new Random();

            foreach (var dev in allowedGpsDevices)
            {
                if (!localVehicles.Any(v => string.Equals(v.DeviceNum, dev.Imei, StringComparison.OrdinalIgnoreCase)))
                {
                    var newVehicle = new Vehicle
                    {
                        Id = Guid.NewGuid(),
                        DeviceNum = dev.Imei,
                        RegName = dev.Asset?.Name ?? ("GPS Tracker " + (dev.Imei.Length > 4 ? dev.Imei.Substring(dev.Imei.Length - 4) : dev.Imei)),
                        Status = dev.Status == Domain.Enums.DeviceStatus.Online ? "Online" : "Offline",
                        Lat = 18.6203 + (rand.NextDouble() - 0.5) * 0.003,
                        Lon = 73.8567 + (rand.NextDouble() - 0.5) * 0.003,
                        Speed = dev.Status == Domain.Enums.DeviceStatus.Online ? 15.0 : 0.0,
                        Direction = 90.0,
                        Battery = dev.BatteryLevel,
                        GpsTime = DateTime.UtcNow,
                        UpdateTime = DateTime.UtcNow,
                        CreatedOn = DateTime.UtcNow,
                        CreatedBy = dev.CreatedBy ?? currentEmail ?? currentUserId
                    };
                    _db.Vehicles.Add(newVehicle);
                    localVehicles.Add(newVehicle);
                    changed = true;
                }
            }

            if (changed)
            {
                await _db.SaveChangesAsync();
            }

            return Ok(localVehicles);
        }
    }
}
