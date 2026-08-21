using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests
{
    public class ReportVerificationTests
    {
        private AssetTrackingDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AssetTrackingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            var context = new AssetTrackingDbContext(options);

            // 1. Seed Site
            var site = new Site { Id = Guid.NewGuid(), Name = "Alpha Project", Code = "SITE-01" };
            context.Sites.Add(site);

            // 2. Seed Category
            var cat = new AssetCategory { Id = Guid.NewGuid(), Name = "IT Equipment" };
            context.AssetCategories.Add(cat);

            // 3. Seed Assets
            var asset1 = new Asset { Id = Guid.NewGuid(), AssetNumber = "AST-1001", Name = "Dell Laptop", AssetCategoryId = cat.Id, AssetCategory = cat, SiteId = site.Id, Site = site, CreatedOn = DateTime.UtcNow.AddDays(-5) };
            var asset2 = new Asset { Id = Guid.NewGuid(), AssetNumber = "AST-1002", Name = "Forklift", AssetCategoryId = cat.Id, AssetCategory = cat, CreatedOn = DateTime.UtcNow.AddDays(-25) };
            context.Assets.AddRange(asset1, asset2);

            // 4. Seed Issuance
            var issuance = new AssetIssuance
            {
                Id = Guid.NewGuid(),
                IssueCode = "ISS-1001",
                AssetId = asset1.Id,
                AssetNumber = asset1.AssetNumber,
                AssetName = asset1.Name,
                IssuedToPerson = "Rajesh Kumar",
                Contractor = "ABC Infra Ltd",
                IssueQuantity = 5,
                Unit = "Pcs",
                Purpose = "Foundation Slab Work",
                SiteId = site.Id,
                Site = site,
                SiteName = site.Name,
                IssuedDate = DateTime.UtcNow.AddDays(-2)
            };
            context.AssetIssuances.Add(issuance);

            // 5. Seed Transfer
            var site2 = new Site { Id = Guid.NewGuid(), Name = "Beta Warehouse", Code = "SITE-02" };
            context.Sites.Add(site2);
            var transfer = new AssetTransfer
            {
                Id = Guid.NewGuid(),
                AssetId = asset1.Id,
                Asset = asset1,
                ItemName = asset1.Name,
                SourceSiteId = site.Id,
                SourceSite = site,
                DestinationSiteId = site2.Id,
                DestinationSite = site2,
                Quantity = 1,
                TransferDate = DateTime.UtcNow.AddDays(-3),
                RequestedByUserId = Guid.NewGuid(),
                RequestedByUser = new User { Id = Guid.NewGuid(), Username = "admin", Email = "admin@test.com", PasswordHash = "hash", PasswordSalt = "salt" }
            };
            context.AssetTransfers.Add(transfer);

            // 6. Seed RFID Tag
            var tag = new RFIDTag
            {
                Id = Guid.NewGuid(),
                EpcCode = "E28011606000021A3B01",
                TidCode = "E200341201380000",
                AssetId = asset1.Id,
                Asset = asset1,
                CreatedOn = DateTime.UtcNow.AddDays(-5)
            };
            context.RFIDTags.Add(tag);

            context.SaveChanges();
            return context;
        }

        [Fact]
        public async Task Test_AssetReport_FilterBySiteAndDate()
        {
            var db = GetInMemoryDbContext();
            var service = new ReportService(db);

            // 1. All assets
            var allBytes = await service.GenerateAssetReportAsync("csv");
            var allCsv = Encoding.UTF8.GetString(allBytes);
            Assert.Contains("AST-1001", allCsv);
            Assert.Contains("AST-1002", allCsv);
            Assert.Contains("Site Name", allCsv);

            // 2. Filter by Site Name
            var siteBytes = await service.GenerateAssetReportAsync("csv", siteName: "Alpha Project");
            var siteCsv = Encoding.UTF8.GetString(siteBytes);
            Assert.Contains("AST-1001", siteCsv);
            Assert.DoesNotContain("AST-1002", siteCsv);

            // 3. Filter by Date Range
            var dateBytes = await service.GenerateAssetReportAsync("csv", startDate: DateTime.UtcNow.AddDays(-10), endDate: DateTime.UtcNow);
            var dateCsv = Encoding.UTF8.GetString(dateBytes);
            Assert.Contains("AST-1001", dateCsv);
            Assert.DoesNotContain("AST-1002", dateCsv);
        }

        [Fact]
        public async Task Test_TransferReport_Verification()
        {
            var db = GetInMemoryDbContext();
            var service = new ReportService(db);

            var bytes = await service.GenerateTransferReportAsync("csv");
            var csv = Encoding.UTF8.GetString(bytes);
            Assert.Contains("AST-1001", csv);
            Assert.Contains("Alpha Project", csv);
            Assert.Contains("Beta Warehouse", csv);
            Assert.Contains("Transfer Date", csv);
        }

        [Fact]
        public async Task Test_IssuanceReport_Verification()
        {
            var db = GetInMemoryDbContext();
            var service = new ReportService(db);

            var bytes = await service.GenerateIssuanceReportAsync("csv");
            var csv = Encoding.UTF8.GetString(bytes);
            Assert.Contains("ISS-1001", csv);
            Assert.Contains("ABC Infra Ltd", csv);
            Assert.Contains("Rajesh Kumar", csv);
            Assert.Contains("Alpha Project", csv);
            Assert.Contains("Foundation Slab Work", csv);
        }

        [Fact]
        public async Task Test_RFIDReport_Verification()
        {
            var db = GetInMemoryDbContext();
            var service = new ReportService(db);

            var bytes = await service.GenerateRFIDReportAsync("csv", siteName: "Alpha Project");
            var csv = Encoding.UTF8.GetString(bytes);
            Assert.Contains("E28011606000021A3B01", csv);
            Assert.Contains("AST-1001", csv);
            Assert.Contains("Alpha Project", csv);
        }
    }
}
