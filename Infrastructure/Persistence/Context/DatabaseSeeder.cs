using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Context
{
    public static class DatabaseSeeder
    {
        public static readonly Guid ReturnableContainerId = Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d");
        public static readonly Guid MaterialHandlingEquipmentId = Guid.Parse("b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e");
        public static readonly Guid ITAssetsId = Guid.Parse("c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f");
        public static readonly Guid VehiclesId = Guid.Parse("d4e5f6a7-b8c9-0d1e-2f3a-4b5c6d7e8f9a");
        public static readonly Guid PowerEquipmentId = Guid.Parse("e5f6a7b8-c9d0-1e2f-3a4b-5c6d7e8f9a0b");
        public static readonly Guid MaterialHandlingId = Guid.Parse("f6a7b8c9-d0e1-2f3a-4b5c-6d7e8f9a0b1c");
        public static readonly Guid ConsumablesId = Guid.Parse("a7b8c9d0-e1f2-3a4b-5c6d-7e8f9a0b1c2d");

        public static async Task SeedAsync(AssetTrackingDbContext context)
        {
            // 1. Seed Roles
            var rolesToSeed = new System.Collections.Generic.List<Role>
            {
                new Role { Id = Guid.Parse("e1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62"), Name = "Super Admin", Description = "System Administrator with full access across all sites" },
                new Role { Id = Guid.Parse("e2a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62"), Name = "Site Admin", Description = "Site Administrator restricted to their assigned site" },
                new Role { Id = Guid.Parse("e3a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62"), Name = "Supervisor", Description = "Yard Supervisor restricted to operations at their assigned site" },
                new Role { Id = Guid.Parse("e4a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62"), Name = "Driver", Description = "Vehicle Driver with restricted access to mobile GPS operations" },
                new Role { Id = Guid.Parse("e5a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c62"), Name = "Viewer", Description = "Read-only access to dashboard data" }
            };

            foreach (var r in rolesToSeed)
            {
                if (!await context.Roles.AnyAsync(existing => existing.Id == r.Id || existing.Name == r.Name))
                {
                    await context.Roles.AddAsync(r);
                }
            }
            await context.SaveChangesAsync();

            // 2. Seed Permissions
            if (!await context.Permissions.AnyAsync())
            {
                var permissions = new[]
                {
                    new Permission { Id = Guid.NewGuid(), Name = "Create Assets", Code = "assets:create" },
                    new Permission { Id = Guid.NewGuid(), Name = "Read Assets", Code = "assets:read" },
                    new Permission { Id = Guid.NewGuid(), Name = "Update Assets", Code = "assets:update" },
                    new Permission { Id = Guid.NewGuid(), Name = "Delete Assets", Code = "assets:delete" },
                    new Permission { Id = Guid.NewGuid(), Name = "View Reports", Code = "reports:view" },
                    new Permission { Id = Guid.NewGuid(), Name = "Manage Settings", Code = "settings:manage" }
                };

                await context.Permissions.AddRangeAsync(permissions);
                await context.SaveChangesAsync();

                var adminRole = await context.Roles.FirstAsync(r => r.Name == "Super Admin");
                foreach (var p in permissions)
                {
                    await context.RolePermissions.AddAsync(new RolePermission { RoleId = adminRole.Id, PermissionId = p.Id });
                }
                await context.SaveChangesAsync();
            }

            // 3. Seed Users (Admin, Supervisor, Operator with password 123456)
            var saltBytes = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var saltStr = Convert.ToBase64String(saltBytes);
            
            using var rfc2898 = new Rfc2898DeriveBytes("123456", saltBytes, 10000, HashAlgorithmName.SHA256);
            var hashBytes = rfc2898.GetBytes(32);
            var hashStr = Convert.ToBase64String(hashBytes);

            if (!await context.Users.AnyAsync(u => u.Email == "trackit@prosper.com" || u.Username == "admin"))
            {
                var adminUser = new User
                {
                    Id = Guid.Parse("e1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c6d"),
                    Username = "admin",
                    Email = "trackit@prosper.com",
                    PasswordHash = hashStr,
                    PasswordSalt = saltStr,
                    IsActive = true,
                    SiteId = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c91")
                };
                await context.Users.AddAsync(adminUser);
                await context.SaveChangesAsync();

                var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Super Admin");
                if (adminRole != null)
                {
                    await context.UserRoles.AddAsync(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
                    await context.SaveChangesAsync();
                }
            }

            if (!await context.Users.AnyAsync(u => u.Email == "operator@prosper.com" || u.Username == "operator"))
            {
                var opUser = new User
                {
                    Id = Guid.Parse("e2a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c6e"),
                    Username = "operator",
                    Email = "operator@prosper.com",
                    PasswordHash = hashStr,
                    PasswordSalt = saltStr,
                    IsActive = true,
                    SiteId = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c91")
                };
                await context.Users.AddAsync(opUser);
                await context.SaveChangesAsync();

                var supervisorRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Supervisor") ?? await context.Roles.FirstAsync();
                await context.UserRoles.AddAsync(new UserRole { UserId = opUser.Id, RoleId = supervisorRole.Id });
                await context.SaveChangesAsync();
            }

            // 4. Seed Categories
            var categoriesToSeed = new System.Collections.Generic.List<AssetCategory>
            {
                new AssetCategory { Id = ITAssetsId, Name = "IT Assets", Description = "Laptops, servers, workstations, routers, tablets" },
                new AssetCategory { Id = VehiclesId, Name = "Vehicle", Description = "Company trucks, forklifts, cars, buses, trailers, excavators" },
                new AssetCategory { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c10"), Name = "Medical Equipment", Description = "Diagnostic and clinical machines" },
                new AssetCategory { Id = ReturnableContainerId, Name = "Returnable Container", Description = "Reusable transit items, crates, bins, pallets" },
                new AssetCategory { Id = PowerEquipmentId, Name = "Power Equipment", Description = "Generators, industrial power backup systems" },
                new AssetCategory { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c11"), Name = "Furniture", Description = "Desks, chairs, cabinets, racks" },
                new AssetCategory { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c12"), Name = "Production Machine", Description = "Manufacturing lines and assembly systems" },
                new AssetCategory { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c13"), Name = "Raw Material", Description = "Production inventory and raw materials" },
                new AssetCategory { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c14"), Name = "Tools", Description = "Handheld and industrial tools" },
                new AssetCategory { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c15"), Name = "Pallets", Description = "Standard and customized warehouse pallets" },
                new AssetCategory { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c16"), Name = "Containers", Description = "Shipping containers and staging tanks" },
                new AssetCategory { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c17"), Name = "Documents", Description = "Files, folders, and physical contract folders" },
                new AssetCategory { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c18"), Name = "Safety Equipment", Description = "PPE, helmets, vests, and safety devices" }
            };

            foreach (var cat in categoriesToSeed)
            {
                if (!await context.AssetCategories.AnyAsync(existing => existing.Id == cat.Id || existing.Name == cat.Name))
                {
                    await context.AssetCategories.AddAsync(cat);
                }
            }
            await context.SaveChangesAsync();

            if (!await context.Vehicles.AnyAsync())
            {
                await context.Vehicles.AddRangeAsync(
                    new Vehicle
                    {
                        Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c71"),
                        DeviceNum = "16512010049",
                        RegName = "Toyota Forklift FL-03",
                        Status = "Online",
                        Lat = 18.620321,
                        Lon = 73.856742,
                        Speed = 12,
                        Direction = 45,
                        Battery = 78,
                        GpsTime = DateTime.UtcNow,
                        UpdateTime = DateTime.UtcNow,
                        CreatedOn = DateTime.UtcNow
                    },
                    new Vehicle
                    {
                        Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c72"),
                        DeviceNum = "16512010050",
                        RegName = "Tata Truck TRK-07",
                        Status = "Online",
                        Lat = 18.621589,
                        Lon = 73.858122,
                        Speed = 28,
                        Direction = 180,
                        Battery = 88,
                        GpsTime = DateTime.UtcNow,
                        UpdateTime = DateTime.UtcNow,
                        CreatedOn = DateTime.UtcNow
                    }
                );
                await context.SaveChangesAsync();
            }

            if (!await context.Sites.AnyAsync())
            {
                var pune = new Site { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c91"), Code = "PUNE-DC", Name = "Pune DC", Address = "Pune, Maharashtra, India" };
                var mumbai = new Site { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c92"), Code = "MUM-WH", Name = "Mumbai Warehouse", Address = "Mumbai, Maharashtra, India" };
                var chennai = new Site { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c93"), Code = "CHEN-PLT", Name = "Chennai Plant", Address = "Chennai, Tamil Nadu, India" };
                var bengaluru = new Site { Id = Guid.Parse("f1a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c94"), Code = "BLR-HUB", Name = "Bengaluru Hub", Address = "Bengaluru, Karnataka, India" };

                await context.Sites.AddRangeAsync(pune, mumbai, chennai, bengaluru);
                await context.SaveChangesAsync();

                var puneWh = new Warehouse { Id = Guid.Parse("f2a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c91"), Code = "PUNE-WH-1", Name = "Pune DC Whse", SiteId = pune.Id };
                var mumbaiWh = new Warehouse { Id = Guid.Parse("f2a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c92"), Code = "MUM-WH-1", Name = "Mumbai Whse", SiteId = mumbai.Id };
                var chennaiWh = new Warehouse { Id = Guid.Parse("f2a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c93"), Code = "CHEN-WH-1", Name = "Chennai Whse", SiteId = chennai.Id };
                var bengaluruWh = new Warehouse { Id = Guid.Parse("f2a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c94"), Code = "BLR-WH-1", Name = "Bengaluru Whse", SiteId = bengaluru.Id };

                await context.Warehouses.AddRangeAsync(puneWh, mumbaiWh, chennaiWh, bengaluruWh);
                await context.SaveChangesAsync();
            }

            // Ensure Devam sites exist in DB
            var devamAlphaId = Guid.Parse("019fef88-a629-79e7-af95-546fdb11b7a3");
            var devamProjectId = Guid.Parse("019fef93-7e50-7b70-88e9-6a451cb52b8b");
            var devamWhId = Guid.Parse("019fef93-8ac8-797e-986e-3fcb5e1295b2");

            if (!await context.Sites.AnyAsync(s => s.Id == devamAlphaId))
            {
                await context.Sites.AddAsync(new Site { Id = devamAlphaId, Code = "DEVAM-ALPHA", Name = "Devam Central Store Site Alpha", Address = "Devam Alpha Complex, Pune" });
            }
            if (!await context.Sites.AnyAsync(s => s.Id == devamProjectId))
            {
                await context.Sites.AddAsync(new Site { Id = devamProjectId, Code = "DEVAM-PROJ", Name = "Devam Central Store Project Site", Address = "Devam Project Site, Mumbai" });
            }
            await context.SaveChangesAsync();

            if (!await context.Warehouses.AnyAsync(w => w.Id == devamWhId))
            {
                await context.Warehouses.AddAsync(new Warehouse { Id = devamWhId, Code = "DEVAM-WH-1", Name = "Devam Central Store Main Warehouse", SiteId = devamAlphaId, Address = "Devam Main Storage" });
                await context.SaveChangesAsync();
            }

            // Seed Assets for Devam Sites if Assets table has no assets for Alpha / Project
            if (!await context.Assets.AnyAsync(a => a.SiteId == devamAlphaId))
            {
                await context.Assets.AddRangeAsync(
                    new Asset { Id = Guid.NewGuid(), AssetNumber = "AST-ALPHA-001", Name = "High Performance Server Rack - Alpha", Status = AssetStatus.Available, AssetCategoryId = ITAssetsId, SiteId = devamAlphaId, WarehouseId = devamWhId, Group = "IT Infrastructure" },
                    new Asset { Id = Guid.NewGuid(), AssetNumber = "AST-ALPHA-002", Name = "Forklift Heavy Lifter 5T - Alpha", Status = AssetStatus.Assigned, AssetCategoryId = VehiclesId, SiteId = devamAlphaId, WarehouseId = devamWhId, Group = "Material Handling" },
                    new Asset { Id = Guid.NewGuid(), AssetNumber = "AST-ALPHA-003", Name = "Industrial Generator 500kVA", Status = AssetStatus.Available, AssetCategoryId = PowerEquipmentId, SiteId = devamAlphaId, WarehouseId = devamWhId, Group = "Power Equipment" },
                    new Asset { Id = Guid.NewGuid(), AssetNumber = "AST-ALPHA-004", Name = "Returnable Transit Pallet Bin #104", Status = AssetStatus.UnderMaintenance, AssetCategoryId = ReturnableContainerId, SiteId = devamAlphaId, WarehouseId = devamWhId, Group = "Returnables" }
                );
                await context.SaveChangesAsync();
            }

            if (!await context.Assets.AnyAsync(a => a.SiteId == devamProjectId))
            {
                await context.Assets.AddRangeAsync(
                    new Asset { Id = Guid.NewGuid(), AssetNumber = "AST-PROJ-001", Name = "Mobile Crane 20T - Project Site", Status = AssetStatus.Assigned, AssetCategoryId = VehiclesId, SiteId = devamProjectId, Group = "Heavy Equipment" },
                    new Asset { Id = Guid.NewGuid(), AssetNumber = "AST-PROJ-002", Name = "Site Field Workstation Laptop #02", Status = AssetStatus.Available, AssetCategoryId = ITAssetsId, SiteId = devamProjectId, Group = "IT Equipment" },
                    new Asset { Id = Guid.NewGuid(), AssetNumber = "AST-PROJ-003", Name = "Project Site Container Store #05", Status = AssetStatus.Available, AssetCategoryId = ReturnableContainerId, SiteId = devamProjectId, Group = "Containers" }
                );
                await context.SaveChangesAsync();
            // Ensure all Devam Alpha Site assets are assigned to Devam Warehouse
            var alphaAssets = await context.Assets.Where(a => a.SiteId == devamAlphaId && a.WarehouseId == null).ToListAsync();
            if (alphaAssets.Any())
            {
                foreach (var a in alphaAssets)
                {
                    a.WarehouseId = devamWhId;
                }
                await context.SaveChangesAsync();
            }

            // 5. Seed default Handheld Device
            if (!await context.HandheldDevices.AnyAsync())
            {
                var handheld = new HandheldDevice
                {
                    Id = Guid.Parse("f3a2b3c4-d5e6-7a8b-9c0d-1e2f3a4b5c71"),
                    Name = "HH-01",
                    DeviceSerial = "HC72BC250900278",
                    Model = "Chainway C72",
                    Status = DeviceStatus.Online,
                    CreatedOn = DateTime.UtcNow
                };
                await context.HandheldDevices.AddAsync(handheld);
                await context.SaveChangesAsync();
            }
        }
    }
}
