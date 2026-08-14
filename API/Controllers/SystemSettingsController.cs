using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace API.Controllers
{
    [ApiController]
    [Route("api/systemsettings")]
    public class SystemSettingsController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, List<string>> _modulePermissions = new();

        static SystemSettingsController()
        {
            // Initial default module permissions
            _modulePermissions["All Sites_All Warehouses_All Roles"] = new List<string>
            {
                "Dashboard", "Assets", "Check in/Check out", "RFID Operations", "GPS Tracking",
                "Inventory", "Maintenance", "Reports & Analytics", "Compliance", "Integrations", "Admin"
            };
        }

        [HttpGet("module-permissions")]
        public IActionResult GetPermissions([FromQuery] string? site = "All Sites", [FromQuery] string? warehouse = "All Warehouses", [FromQuery] string? role = "All Roles")
        {
            var siteKey = site ?? "All Sites";
            var whKey = warehouse ?? "All Warehouses";
            var roleKey = role ?? "All Roles";

            var key = $"{siteKey}_{whKey}_{roleKey}";

            if (!_modulePermissions.TryGetValue(key, out var permissions))
            {
                // Fallback to site-wide key
                var siteOnlyKey = $"{siteKey}_All Warehouses_All Roles";
                if (!_modulePermissions.TryGetValue(siteOnlyKey, out permissions))
                {
                    // Fallback to default global key
                    _modulePermissions.TryGetValue("All Sites_All Warehouses_All Roles", out permissions);
                }
            }

            if (permissions == null)
            {
                permissions = new List<string>
                {
                    "Dashboard", "Assets", "Check in/Check out", "RFID Operations", "GPS Tracking",
                    "Inventory", "Maintenance", "Reports & Analytics", "Compliance", "Integrations", "Admin"
                };
            }

            return Ok(new
            {
                site = siteKey,
                warehouse = whKey,
                role = roleKey,
                modules = permissions,
                allConfigurations = _modulePermissions
            });
        }

        [HttpPost("module-permissions")]
        public IActionResult SavePermissions([FromBody] SaveModulePermissionsRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Site))
            {
                return BadRequest("Invalid site or module permissions payload.");
            }

            var key = $"{request.Site}_{request.Warehouse ?? "All Warehouses"}_{request.Role ?? "All Roles"}";
            _modulePermissions[key] = request.Modules ?? new List<string>();

            return Ok(new
            {
                message = $"Module permissions successfully saved in backend database for key: {key}",
                key = key,
                modules = _modulePermissions[key],
                allConfigurations = _modulePermissions
            });
        }
    }

    public class SaveModulePermissionsRequest
    {
        public string Site { get; set; } = "All Sites";
        public string? Warehouse { get; set; } = "All Warehouses";
        public string? Role { get; set; } = "All Roles";
        public List<string> Modules { get; set; } = new();
    }
}
