using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/systemsettings")]
    public class SystemSettingsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly ConcurrentDictionary<string, List<string>> _modulePermissions = new();
        private static readonly object _initLock = new();
        private static bool _isInitialized = false;

        private static readonly List<string> DefaultAllModules = new()
        {
            "Dashboard", "Assets", "Check in/Check out", "RFID Operations", "GPS Tracking",
            "Inventory", "Maintenance", "Reports & Analytics", "Compliance", "Integrations", "Admin"
        };

        public SystemSettingsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_isInitialized) return;
            lock (_initLock)
            {
                if (_isInitialized) return;
                try
                {
                    var settingsRepo = _unitOfWork.Repository<Settings>();
                    var allSettings = settingsRepo.GetAllAsync().GetAwaiter().GetResult();
                    var moduleSettings = allSettings.Where(s => s.Group == "ModuleAccess" && s.Key.StartsWith("ModulePermissions_"));

                    foreach (var s in moduleSettings)
                    {
                        var rawKey = s.Key.Substring("ModulePermissions_".Length);
                        try
                        {
                            var list = JsonSerializer.Deserialize<List<string>>(s.Value);
                            if (list != null)
                            {
                                _modulePermissions[rawKey] = list;
                            }
                        }
                        catch { }
                    }

                    if (!_modulePermissions.ContainsKey("All Sites_All Warehouses_All Roles"))
                    {
                        _modulePermissions["All Sites_All Warehouses_All Roles"] = new List<string>(DefaultAllModules);
                    }
                }
                catch
                {
                    _modulePermissions["All Sites_All Warehouses_All Roles"] = new List<string>(DefaultAllModules);
                }
                _isInitialized = true;
            }
        }

        [HttpGet("module-permissions")]
        public async Task<IActionResult> GetPermissions(
            [FromQuery] string? site = "All Sites", 
            [FromQuery] string? warehouse = "All Warehouses", 
            [FromQuery] string? role = "All Roles",
            CancellationToken cancellationToken = default)
        {
            var siteKey = string.IsNullOrWhiteSpace(site) ? "All Sites" : site.Trim();
            var whKey = string.IsNullOrWhiteSpace(warehouse) ? "All Warehouses" : warehouse.Trim();
            var roleKey = string.IsNullOrWhiteSpace(role) ? "All Roles" : role.Trim();

            var key = $"{siteKey}_{whKey}_{roleKey}";

            if (!_modulePermissions.TryGetValue(key, out var permissions))
            {
                // Fallback: check database directly in case another instance updated it
                var settingKey = $"ModulePermissions_{key}";
                var settings = await _unitOfWork.Repository<Settings>().GetFilteredAsync(s => s.Key == settingKey, cancellationToken);
                var setting = settings.FirstOrDefault();
                if (setting != null)
                {
                    try
                    {
                        permissions = JsonSerializer.Deserialize<List<string>>(setting.Value);
                        if (permissions != null) _modulePermissions[key] = permissions;
                    }
                    catch { }
                }
            }

            if (permissions == null)
            {
                // Fallback to site-wide key
                var siteOnlyKey = $"{siteKey}_All Warehouses_All Roles";
                if (!_modulePermissions.TryGetValue(siteOnlyKey, out permissions))
                {
                    // Fallback to default global key
                    if (!_modulePermissions.TryGetValue("All Sites_All Warehouses_All Roles", out permissions))
                    {
                        permissions = new List<string>(DefaultAllModules);
                    }
                }
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
        public async Task<IActionResult> SavePermissions(
            [FromBody] SaveModulePermissionsRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Site))
            {
                return BadRequest("Invalid site or module permissions payload.");
            }

            var siteKey = request.Site.Trim();
            var whKey = string.IsNullOrWhiteSpace(request.Warehouse) ? "All Warehouses" : request.Warehouse.Trim();
            var roleKey = string.IsNullOrWhiteSpace(request.Role) ? "All Roles" : request.Role.Trim();

            var key = $"{siteKey}_{whKey}_{roleKey}";
            var modules = request.Modules ?? new List<string>();
            _modulePermissions[key] = modules;

            var settingKey = $"ModulePermissions_{key}";
            var settingsRepo = _unitOfWork.Repository<Settings>();
            var existingList = await settingsRepo.GetFilteredAsync(s => s.Key == settingKey, cancellationToken);
            var existing = existingList.FirstOrDefault();

            var jsonValue = JsonSerializer.Serialize(modules);

            if (existing != null)
            {
                existing.Value = jsonValue;
                existing.Group = "ModuleAccess";
                existing.Description = $"Module access permissions for Site: {siteKey}, Warehouse: {whKey}, Role: {roleKey}";
                settingsRepo.Update(existing);
            }
            else
            {
                var newSetting = new Settings
                {
                    Key = settingKey,
                    Value = jsonValue,
                    Group = "ModuleAccess",
                    Description = $"Module access permissions for Site: {siteKey}, Warehouse: {whKey}, Role: {roleKey}"
                };
                await settingsRepo.AddAsync(newSetting, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = $"Module permissions successfully saved in backend database for key: {key}",
                key = key,
                modules = modules,
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
