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
                vehicle.Status = "ACC ON,LBS";
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
            var list = await _db.Vehicles.ToListAsync();
            return Ok(list);
        }
    }
}
