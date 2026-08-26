using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private HttpContext? HttpContext => _httpContextAccessor.HttpContext;

        public bool IsAuthenticated => HttpContext?.User?.Identity?.IsAuthenticated == true;

        public Guid? UserId
        {
            get
            {
                var claim = HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? HttpContext?.User?.FindFirst("sub")?.Value;
                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        public string? Username =>
            HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value
            ?? HttpContext?.User?.FindFirst("unique_name")?.Value
            ?? HttpContext?.User?.FindFirst("username")?.Value;

        public string? Email =>
            HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
            ?? HttpContext?.User?.FindFirst("email")?.Value;

        public Guid? SiteId
        {
            get
            {
                // 1. From header override if provided
                if (HttpContext?.Request?.Headers.TryGetValue("X-Site-Id", out var headerVal) == true)
                {
                    if (Guid.TryParse(headerVal.FirstOrDefault(), out var headerGuid) && headerGuid != Guid.Empty)
                        return headerGuid;
                }

                // 2. From JWT claims
                var claim = HttpContext?.User?.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);

                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        public Guid? WarehouseId
        {
            get
            {
                // 1. From header override if provided
                if (HttpContext?.Request?.Headers.TryGetValue("X-Warehouse-Id", out var headerVal) == true)
                {
                    if (Guid.TryParse(headerVal.FirstOrDefault(), out var headerGuid) && headerGuid != Guid.Empty)
                        return headerGuid;
                }

                // 2. From JWT claims
                var claim = HttpContext?.User?.Claims
                    .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);

                return Guid.TryParse(claim, out var guid) ? guid : null;
            }
        }

        public string? ActiveRole
        {
            get
            {
                if (HttpContext?.Request?.Headers.TryGetValue("X-Role", out var headerVal) == true && !string.IsNullOrWhiteSpace(headerVal))
                {
                    return headerVal.ToString();
                }

                return Roles.FirstOrDefault();
            }
        }

        public List<string> Roles
        {
            get
            {
                if (HttpContext?.User?.Claims == null) return new List<string>();

                var roles = HttpContext.User.Claims
                    .Where(c => c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
                    .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Distinct()
                    .ToList();

                return roles;
            }
        }

        public List<Guid> AllowedSiteIds
        {
            get
            {
                if (HttpContext?.User?.Claims == null) return new List<Guid>();

                var guids = new HashSet<Guid>();
                foreach (var claim in HttpContext.User.Claims.Where(c => c.Type == "sites" || c.Type == "siteId" || c.Type == "site_id" || c.Type == "allowed_site_ids" || c.Type == "allowed_site_ids_csv"))
                {
                    foreach (var part in claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Guid.TryParse(part.Trim(), out var g) && g != Guid.Empty)
                            guids.Add(g);
                    }
                }

                return guids.ToList();
            }
        }

        public List<Guid> AllowedWarehouseIds
        {
            get
            {
                if (HttpContext?.User?.Claims == null) return new List<Guid>();

                var guids = new HashSet<Guid>();
                foreach (var claim in HttpContext.User.Claims.Where(c => c.Type == "warehouses" || c.Type == "warehouseId" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids" || c.Type == "allowed_warehouse_ids_csv"))
                {
                    foreach (var part in claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Guid.TryParse(part.Trim(), out var g) && g != Guid.Empty)
                            guids.Add(g);
                    }
                }

                return guids.ToList();
            }
        }

        public bool IsSuperAdmin
        {
            get
            {
                if (HttpContext?.User == null) return false;

                if (HttpContext.User.IsInRole("Super Admin") || HttpContext.User.IsInRole("System Administrator") || HttpContext.User.IsInRole("SuperAdmin"))
                    return true;

                if (Roles.Any(r => r.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) ||
                                   r.Equals("System Administrator", StringComparison.OrdinalIgnoreCase) ||
                                   r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)))
                    return true;

                if (HttpContext.User.HasClaim(c => (c.Type == "allowed_site_ids" && c.Value == "ALL") || (c.Type == "sites" && c.Value == "GLOBAL_ALL_SITES")))
                    return true;

                return false;
            }
        }
    }
}
