using Domain.Entities;
using Infrastructure.Persistence.Context;
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
            // First check if there is a local GPSDevice with this IMEI
            var gpsDevice = await _db.GPSDevices.FirstOrDefaultAsync(g => g.Imei == vehicleId);
            if (gpsDevice != null)
            {
                // Check if we have local history
                var localHistory = await _db.GPSHistories
                    .Where(h => h.GPSDeviceId == gpsDevice.Id && h.Timestamp >= beginGPSTime && h.Timestamp <= endGPSTime)
                    .OrderBy(h => h.Timestamp)
                    .ToListAsync();

                if (localHistory.Any())
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

            return Ok(JsonDocument.Parse(locationJson).RootElement);
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
                vehicle.Status = "Online";
                
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
                    if (batt > 0)
                    {
                        gpsDevice.BatteryLevel = (int)batt;
                    }
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
        public async Task<IActionResult> GetVehicles()
        {
            try
            {
                // 1. Get token
                var token = await GetEzzlocTokenAsync();

                // 2. Get vehicle list
                var vehicleListJson = await PostGpsApiAsync(new
                {
                    cmd = "getVehicleList",
                    token = token,
                    language = 2,
                    @params = new { }
                });

                using var vehicleListDoc = JsonDocument.Parse(vehicleListJson);
                var root = vehicleListDoc.RootElement;

                if (root.TryGetProperty("result", out var resultProp) && resultProp.GetInt32() == 1 &&
                    root.TryGetProperty("detail", out var detailProp) && detailProp.ValueKind == JsonValueKind.Array)
                {
                    var ezzlocVehicles = detailProp.EnumerateArray().ToList();
                    var vids = string.Join(",", ezzlocVehicles
                        .Select(v => v.TryGetProperty("VehicleID", out var vid) ? vid.ToString() : null)
                        .Where(id => id != null));

                    if (!string.IsNullOrEmpty(vids))
                    {
                        // 3. Get real-time locations
                        var locationJson = await PostGpsApiAsync(new
                        {
                            cmd = "getVehiclesLocation",
                            token = token,
                            language = 2,
                            @params = new
                            {
                                VehicleIDs = vids,
                                LastTime = "0"
                            }
                        });

                        using var locationDoc = JsonDocument.Parse(locationJson);
                        var locRoot = locationDoc.RootElement;

                        if (locRoot.TryGetProperty("result", out var locResult) && locResult.GetInt32() == 1 &&
                            locRoot.TryGetProperty("detail", out var locDetail) &&
                            locDetail.TryGetProperty("data", out var dataArray) &&
                            dataArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var locNode in dataArray.EnumerateArray())
                            {
                                if (!locNode.TryGetProperty("DeviceNum", out var deviceNumProp)) continue;
                                var deviceNum = deviceNumProp.GetString();
                                if (string.IsNullOrEmpty(deviceNum)) continue;

                                // Parse Lat and Lon
                                double lat = 0;
                                double lon = 0;
                                if (locNode.TryGetProperty("Lat", out var latProp))
                                    double.TryParse(latProp.GetString() ?? "0", out lat);
                                if (locNode.TryGetProperty("Lon", out var lonProp))
                                    double.TryParse(lonProp.GetString() ?? "0", out lon);

                                // Parse Speed, Direction, Battery, status
                                double speed = 0;
                                if (locNode.TryGetProperty("Speed", out var speedProp))
                                    double.TryParse(speedProp.GetString() ?? "0", out speed);

                                double direction = 0;
                                if (locNode.TryGetProperty("Direction", out var dirProp))
                                    double.TryParse(dirProp.GetString() ?? "0", out direction);

                                double battery = 100;
                                if (locNode.TryGetProperty("Battery", out var batProp))
                                {
                                    var batStr = batProp.GetString();
                                    if (!string.IsNullOrEmpty(batStr))
                                        double.TryParse(batStr, out battery);
                                }

                                string status = "Online";
                                if (locNode.TryGetProperty("OnlineStatus", out var statProp))
                                {
                                    status = statProp.GetString() ?? "Online";
                                }

                                DateTime gpsTime = DateTime.UtcNow;
                                if (locNode.TryGetProperty("GpsTime", out var timeProp))
                                {
                                    if (timeProp.ValueKind == JsonValueKind.Number)
                                    {
                                        long ms = timeProp.GetInt64();
                                        if (ms > 0) gpsTime = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                                    }
                                }

                                // Sync local Vehicles table
                                var dbVehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.DeviceNum == deviceNum);
                                if (dbVehicle == null)
                                {
                                    dbVehicle = new Vehicle
                                    {
                                        Id = Guid.NewGuid(),
                                        DeviceNum = deviceNum,
                                        RegName = deviceNum,
                                        Status = status,
                                        Lat = lat,
                                        Lon = lon,
                                        Speed = speed,
                                        Direction = direction,
                                        Battery = battery,
                                        GpsTime = gpsTime,
                                        UpdateTime = DateTime.UtcNow,
                                        CreatedOn = DateTime.UtcNow
                                    };
                                    _db.Vehicles.Add(dbVehicle);
                                }
                                else
                                {
                                    dbVehicle.Status = status;
                                    dbVehicle.Lat = lat;
                                    dbVehicle.Lon = lon;
                                    dbVehicle.Speed = speed;
                                    dbVehicle.Direction = direction;
                                    dbVehicle.Battery = battery;
                                    dbVehicle.GpsTime = gpsTime;
                                    dbVehicle.UpdateTime = DateTime.UtcNow;
                                    _db.Vehicles.Update(dbVehicle);
                                }

                                // Sync local GPSDevices table
                                var dbDevice = await _db.GPSDevices.FirstOrDefaultAsync(d => d.Imei == deviceNum);
                                if (dbDevice == null)
                                {
                                    dbDevice = new GPSDevice
                                    {
                                        Id = Guid.NewGuid(),
                                        Imei = deviceNum,
                                        SimNumber = "Sim " + deviceNum.Substring(Math.Max(0, deviceNum.Length - 4)),
                                        BatteryLevel = (int)battery,
                                        Status = status.ToLower().Contains("offline") ? Domain.Enums.DeviceStatus.Offline : Domain.Enums.DeviceStatus.Online,
                                        CreatedOn = DateTime.UtcNow
                                    };
                                    _db.GPSDevices.Add(dbDevice);
                                }
                                else
                                {
                                    dbDevice.BatteryLevel = (int)battery;
                                    dbDevice.Status = status.ToLower().Contains("offline") ? Domain.Enums.DeviceStatus.Offline : Domain.Enums.DeviceStatus.Online;
                                    _db.GPSDevices.Update(dbDevice);
                                }

                                // Add to GPSHistories to log path if location changed
                                var lastHist = await _db.GPSHistories
                                    .Where(h => h.GPSDeviceId == dbDevice.Id)
                                    .OrderByDescending(h => h.Timestamp)
                                    .FirstOrDefaultAsync();

                                if (lastHist == null || Math.Abs(lastHist.Latitude - lat) > 0.000001 || Math.Abs(lastHist.Longitude - lon) > 0.000001)
                                {
                                    var history = new GPSHistory
                                    {
                                        Id = Guid.NewGuid(),
                                        GPSDeviceId = dbDevice.Id,
                                        Latitude = lat,
                                        Longitude = lon,
                                        Speed = speed,
                                        Heading = direction,
                                        Timestamp = gpsTime,
                                        CreatedOn = DateTime.UtcNow
                                    };
                                    _db.GPSHistories.Add(history);
                                }
                            }
                            await _db.SaveChangesAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing vehicles from Ezzloc: {ex.Message}");
            }

            var localVehicles = await _db.Vehicles.ToListAsync();
            return Ok(localVehicles);
        }
    }
}
