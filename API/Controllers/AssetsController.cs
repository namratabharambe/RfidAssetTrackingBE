using Application.Assets.Commands.CreateAsset;
using Application.Assets.Commands.DeleteAsset;
using Application.Assets.Commands.UpdateAsset;
using Application.Assets.Queries;
using Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/assets")]
    public class AssetsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AssetsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AssetDto>>> Get(
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? size = null)
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

            var assets = await _mediator.Send(new GetAssetsQuery());

            // Check authenticated user claims
            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var isGlobalSuperAdmin = HttpContext.User.IsInRole("Super Admin") 
                                      || HttpContext.User.IsInRole("System Administrator")
                                      || HttpContext.User.HasClaim(c => c.Type == "allowed_site_ids" && c.Value == "ALL")
                                      || HttpContext.User.HasClaim(c => c.Type == "sites" && c.Value == "GLOBAL_ALL_SITES");

                var allowedSiteGuids = HttpContext.User.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                    .Where(g => g.HasValue && g.Value != Guid.Empty)
                    .Select(g => g!.Value)
                    .Distinct()
                    .ToHashSet();

                var allowedWhGuids = HttpContext.User.Claims
                    .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                    .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                    .Where(g => g.HasValue && g.Value != Guid.Empty)
                    .Select(g => g!.Value)
                    .Distinct()
                    .ToHashSet();

                if (!targetWhId.HasValue && allowedWhGuids.Count == 1)
                {
                    targetWhId = allowedWhGuids.First();
                }

                if (!targetSiteId.HasValue && !targetWhId.HasValue && allowedSiteGuids.Count == 1)
                {
                    targetSiteId = allowedSiteGuids.First();
                }

                if (targetWhId.HasValue)
                {
                    assets = assets.Where(a => a.WarehouseId == targetWhId.Value);
                }
                else if (targetSiteId.HasValue)
                {
                    assets = assets.Where(a => a.SiteId == targetSiteId.Value);
                }
                else if (!isGlobalSuperAdmin)
                {
                    if (allowedWhGuids.Any())
                    {
                        assets = assets.Where(a => a.WarehouseId.HasValue && allowedWhGuids.Contains(a.WarehouseId.Value));
                    }
                    else if (allowedSiteGuids.Any())
                    {
                        assets = assets.Where(a => a.SiteId.HasValue && allowedSiteGuids.Contains(a.SiteId.Value));
                    }
                }
            }
            else
            {
                if (targetWhId.HasValue)
                {
                    assets = assets.Where(a => a.WarehouseId == targetWhId.Value);
                }
                else if (targetSiteId.HasValue)
                {
                    assets = assets.Where(a => a.SiteId == targetSiteId.Value);
                }
            }

            // Pagination support
            if (page.HasValue && size.HasValue && size.Value > 0 && page.Value > 0)
            {
                assets = assets.Skip((page.Value - 1) * size.Value).Take(size.Value);
            }

            return Ok(assets);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AssetDto>> Get(Guid id)
        {
            var result = await _mediator.Send(
                new GetAssetByIdQuery(id));

            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpPost]
        public async Task<Guid> Create([FromBody] CreateAssetCommand command)
        {
            var finalCommand = command;

            // Resolve context from headers or claims
            Guid? ctxSiteId = null;
            Guid? ctxWhId = null;

            if (Request.Headers.TryGetValue("X-Warehouse-Id", out var hWh) && Guid.TryParse(hWh.FirstOrDefault(), out var gWh) && gWh != Guid.Empty)
                ctxWhId = gWh;

            if (Request.Headers.TryGetValue("X-Site-Id", out var hSite) && Guid.TryParse(hSite.FirstOrDefault(), out var gSite) && gSite != Guid.Empty)
                ctxSiteId = gSite;

            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                if (!ctxWhId.HasValue)
                {
                    var whClaim = HttpContext.User.Claims
                        .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                        .Select(c => c.Value)
                        .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);
                    if (Guid.TryParse(whClaim, out var gw)) ctxWhId = gw;
                }

                if (!ctxSiteId.HasValue)
                {
                    var siteClaim = HttpContext.User.Claims
                        .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                        .Select(c => c.Value)
                        .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);
                    if (Guid.TryParse(siteClaim, out var gs)) ctxSiteId = gs;
                }
            }

            if (!finalCommand.WarehouseId.HasValue && ctxWhId.HasValue)
                finalCommand = finalCommand with { WarehouseId = ctxWhId };

            if (!finalCommand.SiteId.HasValue && ctxSiteId.HasValue && !ctxWhId.HasValue)
                finalCommand = finalCommand with { SiteId = ctxSiteId };

            return await _mediator.Send(finalCommand);
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> BulkCreate([FromBody] IEnumerable<CreateAssetCommand> commands)
        {
            var createdIds = new List<Guid>();
            Guid? defaultSiteId = null;
            Guid? defaultWhId = null;

            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var siteClaim = HttpContext.User.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out _));
                if (Guid.TryParse(siteClaim, out var g)) defaultSiteId = g;

                var whClaim = HttpContext.User.Claims
                    .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out _));
                if (Guid.TryParse(whClaim, out var gw)) defaultWhId = gw;
            }

            foreach (var command in commands)
            {
                var finalCommand = command;
                if (!finalCommand.SiteId.HasValue && defaultSiteId.HasValue)
                    finalCommand = finalCommand with { SiteId = defaultSiteId };
                if (!finalCommand.WarehouseId.HasValue && defaultWhId.HasValue)
                    finalCommand = finalCommand with { WarehouseId = defaultWhId };

                var id = await _mediator.Send(finalCommand);
                createdIds.Add(id);
            }
            return Ok(new { count = createdIds.Count, ids = createdIds });
        }

        [HttpPost("import-file")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportFile(
            Microsoft.AspNetCore.Http.IFormFile file,
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var createdIds = new List<Guid>();
            using (var reader = new System.IO.StreamReader(file.OpenReadStream()))
            {
                string? line;
                bool isHeader = true;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (isHeader)
                    {
                        isHeader = false;
                        continue;
                    }

                    var parts = line.Split(',');
                    if (parts.Length < 2) continue;

                    var assetNumber = parts[0].Trim();
                    var name = parts[1].Trim();
                    var serialNumber = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : null;

                    var command = new CreateAssetCommand(
                        AssetNumber: assetNumber,
                        Name: name,
                        AssetCategoryId: Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"),
                        Description: null,
                        SerialNumber: serialNumber,
                        Status: Domain.Enums.AssetStatus.Available,
                        QrCode: null,
                        Group: null,
                        AssetType: "Serialized",
                        OwnerDepartment: null,
                        Industry: null,
                        BusinessUnit: null,
                        CurrentCustodian: null,
                        CustodianEmail: null,
                        Model: null,
                        WarrantyProvider: null,
                        PurchaseDate: null,
                        PurchasePrice: null,
                        WarrantyExpiryDate: null,
                        ManufacturerId: null,
                        SiteId: siteId,
                        ZoneId: null,
                        WarehouseId: warehouseId
                    );

                    var id = await _mediator.Send(command);
                    createdIds.Add(id);
                }
            }

            return Ok(new { success = true, count = createdIds.Count, ids = createdIds });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateAssetDto dto,
            [FromServices] Application.Interfaces.IUnitOfWork unitOfWork,
            CancellationToken cancellationToken)
        {
            var existing = await unitOfWork.Repository<Domain.Entities.Asset>().GetByIdAsync(id, cancellationToken);
            if (existing == null) return NotFound();

            // Validate user context
            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var isSuperAdmin = HttpContext.User.IsInRole("Super Admin") || HttpContext.User.IsInRole("System Administrator");
                if (!isSuperAdmin)
                {
                    var whClaim = HttpContext.User.Claims
                        .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                        .Select(c => c.Value)
                        .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);
                    if (Guid.TryParse(whClaim, out var ctxWh) && existing.WarehouseId.HasValue && existing.WarehouseId.Value != ctxWh)
                    {
                        return NotFound();
                    }

                    var siteClaim = HttpContext.User.Claims
                        .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                        .Select(c => c.Value)
                        .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);
                    if (Guid.TryParse(siteClaim, out var ctxSite) && existing.SiteId.HasValue && existing.SiteId.Value != ctxSite)
                    {
                        return NotFound();
                    }
                }
            }

            Enum.TryParse<Domain.Enums.AssetStatus>(dto.Status, true, out var parsedStatus);

            var command = new UpdateAssetCommand(
                id,
                dto.AssetNumber ?? existing.AssetNumber,
                !string.IsNullOrWhiteSpace(dto.Name) ? dto.Name : existing.Name,
                dto.AssetCategoryId != Guid.Empty ? dto.AssetCategoryId : existing.AssetCategoryId,
                dto.Description ?? existing.Description,
                dto.SerialNumber ?? existing.SerialNumber,
                parsedStatus != 0 ? parsedStatus : existing.Status,
                dto.QrCode ?? existing.QrCode,
                dto.Group ?? existing.Group,
                dto.AssetType ?? existing.AssetType,
                dto.OwnerDepartment ?? existing.OwnerDepartment,
                dto.Industry ?? existing.Industry,
                dto.BusinessUnit ?? existing.BusinessUnit,
                dto.CurrentCustodian ?? existing.CurrentCustodian,
                dto.CustodianEmail ?? existing.CustodianEmail,
                dto.Model ?? existing.Model,
                dto.WarrantyProvider ?? existing.WarrantyProvider,
                dto.PurchaseDate ?? existing.PurchaseDate,
                dto.PurchasePrice ?? existing.PurchasePrice,
                dto.WarrantyExpiryDate ?? existing.WarrantyExpiryDate,
                dto.ManufacturerId ?? existing.ManufacturerId,
                dto.SiteId ?? existing.SiteId,
                dto.ZoneId ?? existing.ZoneId,
                dto.WarehouseId ?? existing.WarehouseId,
                dto.DeliveryChallanNo ?? existing.DeliveryChallanNo,
                dto.InvoiceNumber ?? existing.InvoiceNumber,
                dto.InvoiceDate ?? existing.InvoiceDate,
                dto.PoNumber ?? existing.PoNumber,
                dto.Image ?? existing.Image,
                dto.EntryQty ?? existing.EntryQty,
                dto.IssuedQty ?? existing.IssuedQty,
                dto.BalanceQty ?? dto.BalancedQty ?? existing.BalanceQty,
                dto.BalancedQty ?? dto.BalanceQty ?? existing.BalanceQty,
                dto.Unit ?? dto.UnitQty ?? existing.Unit,
                dto.UnitQty ?? dto.Unit ?? existing.Unit,
                dto.GpsId ?? existing.GpsId,
                dto.RfidTag ?? existing.RfidTag,
                dto.Barcode ?? existing.Barcode
            );

            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(
            Guid id,
            [FromServices] Application.Interfaces.IUnitOfWork unitOfWork,
            CancellationToken cancellationToken)
        {
            var existing = await unitOfWork.Repository<Domain.Entities.Asset>().GetByIdAsync(id, cancellationToken);
            if (existing == null) return NotFound();

            // Validate user context
            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var isSuperAdmin = HttpContext.User.IsInRole("Super Admin") || HttpContext.User.IsInRole("System Administrator");
                if (!isSuperAdmin)
                {
                    var whClaim = HttpContext.User.Claims
                        .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                        .Select(c => c.Value)
                        .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);
                    if (Guid.TryParse(whClaim, out var ctxWh) && existing.WarehouseId.HasValue && existing.WarehouseId.Value != ctxWh)
                    {
                        return NotFound();
                    }

                    var siteClaim = HttpContext.User.Claims
                        .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                        .Select(c => c.Value)
                        .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);
                    if (Guid.TryParse(siteClaim, out var ctxSite) && existing.SiteId.HasValue && existing.SiteId.Value != ctxSite)
                    {
                        return NotFound();
                    }
                }
            }

            await _mediator.Send(new DeleteAssetCommand(id), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// GET /api/assets/asset-codes
        /// Reads the active JWT access token (siteId or warehouseId claims).
        /// Queries the Assets database table for asset numbers scoped to the active Site or Warehouse.
        /// - If token contains Site -> Returns database Asset Numbers for that Site.
        /// - If token contains Warehouse -> Returns database Asset Numbers for that Warehouse.
        /// </summary>
        [HttpGet("asset-codes")]
        public async Task<ActionResult<AssetCodeResponseDto>> GetAssetCodes(
            [FromServices] Application.Interfaces.IUnitOfWork unitOfWork,
            CancellationToken cancellationToken)
        {
            var tokenWhClaim = HttpContext.User.Claims
                .FirstOrDefault(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id")?.Value;

            var tokenSiteClaim = HttpContext.User.Claims
                .FirstOrDefault(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id")?.Value;

            Guid? tokenWhId = Guid.TryParse(tokenWhClaim, out var whG) ? whG : null;
            Guid? tokenSiteId = Guid.TryParse(tokenSiteClaim, out var stG) ? stG : null;

            var assetRepo = unitOfWork.Repository<Domain.Entities.Asset>();
            var siteRepo = unitOfWork.Repository<Domain.Entities.Site>();
            var whRepo = unitOfWork.Repository<Domain.Entities.Warehouse>();

            var allSites = await siteRepo.GetAllAsync(cancellationToken);
            var allWarehouses = await whRepo.GetAllAsync(cancellationToken);

            var options = new List<AssetCodeOptionDto>();
            string contextType = "Global";
            string activeCode = "";
            string activeName = "";

            if (tokenWhId.HasValue)
            {
                contextType = "Warehouse";
                var wh = allWarehouses.FirstOrDefault(w => w.Id == tokenWhId.Value);
                activeName = wh?.Name ?? "Selected Warehouse";

                var dbAssets = await assetRepo.GetFilteredAsync(a => a.WarehouseId == tokenWhId.Value && !a.IsDeleted, cancellationToken);
                foreach (var asset in dbAssets)
                {
                    var code = !string.IsNullOrWhiteSpace(asset.AssetNumber) ? asset.AssetNumber : asset.Id.ToString();
                    options.Add(new AssetCodeOptionDto("Asset", asset.Id, code, asset.Name, $"{code} ({asset.Name})"));
                }

                if (options.Count == 0 && wh != null)
                {
                    var whCode = !string.IsNullOrWhiteSpace(wh.Code) ? wh.Code : wh.Name;
                    options.Add(new AssetCodeOptionDto("Warehouse", wh.Id, whCode, wh.Name, $"Warehouse Code: {whCode} ({wh.Name})"));
                }

                if (options.Count > 0)
                {
                    activeCode = options[0].Code;
                }
            }
            else if (tokenSiteId.HasValue)
            {
                contextType = "Site";
                var site = allSites.FirstOrDefault(s => s.Id == tokenSiteId.Value);
                activeName = site?.Name ?? "Selected Site";

                var dbAssets = await assetRepo.GetFilteredAsync(a => a.SiteId == tokenSiteId.Value && !a.IsDeleted, cancellationToken);
                foreach (var asset in dbAssets)
                {
                    var code = !string.IsNullOrWhiteSpace(asset.AssetNumber) ? asset.AssetNumber : asset.Id.ToString();
                    options.Add(new AssetCodeOptionDto("Asset", asset.Id, code, asset.Name, $"{code} ({asset.Name})"));
                }

                if (options.Count == 0 && site != null)
                {
                    var siteCode = !string.IsNullOrWhiteSpace(site.Code) ? site.Code : site.Name;
                    options.Add(new AssetCodeOptionDto("Site", site.Id, siteCode, site.Name, $"Site Number: {siteCode} ({site.Name})"));
                }

                if (options.Count > 0)
                {
                    activeCode = options[0].Code;
                }
            }
            else
            {
                contextType = "Global";
                var dbAssets = await assetRepo.GetFilteredAsync(a => !a.IsDeleted, cancellationToken);
                foreach (var asset in dbAssets.Take(200))
                {
                    var code = !string.IsNullOrWhiteSpace(asset.AssetNumber) ? asset.AssetNumber : asset.Id.ToString();
                    options.Add(new AssetCodeOptionDto("Asset", asset.Id, code, asset.Name, $"{code} ({asset.Name})"));
                }

                if (options.Count > 0)
                {
                    activeCode = options[0].Code;
                    activeName = options[0].Name;
                }
            }

            return Ok(new AssetCodeResponseDto(contextType, activeCode, activeName, options));
        }

        [HttpPost("transfer/central-to-site")]
        public async Task<IActionResult> CentralToSiteTransfer(
            [FromBody] CentralToSiteTransferDto request,
            [FromServices] Application.Interfaces.IUnitOfWork unitOfWork,
            CancellationToken cancellationToken)
        {
            var assetRepo = unitOfWork.Repository<Domain.Entities.Asset>();
            var siteRepo = unitOfWork.Repository<Domain.Entities.Site>();
            var whRepo = unitOfWork.Repository<Domain.Entities.Warehouse>();
            
            var asset = (await assetRepo.GetFilteredAsync(a => a.AssetNumber == request.assetCode || a.Name == request.assetName || !a.IsDeleted, cancellationToken)).FirstOrDefault();
            var firstSiteId = (await siteRepo.GetAllAsync(cancellationToken)).First().Id;

            Guid srcSiteId = (await siteRepo.GetByIdAsync(request.fromWarehouseId, cancellationToken)) != null ? request.fromWarehouseId : firstSiteId;
            Guid dstSiteId = (await siteRepo.GetByIdAsync(request.toSiteId, cancellationToken)) != null ? request.toSiteId : firstSiteId;

            var userClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid currentUserId = Guid.TryParse(userClaim, out var uG) ? uG : (await unitOfWork.Repository<Domain.Entities.User>().GetAllAsync(cancellationToken)).First().Id;

            var transfer = new Domain.Entities.AssetTransfer
            {
                AssetId = asset != null ? asset.Id : (await assetRepo.GetAllAsync(cancellationToken)).First().Id,
                ItemName = request.assetName ?? asset?.Name ?? "Galvanized Steel Pipes",
                SourceSiteId = srcSiteId,
                DestinationSiteId = dstSiteId,
                Quantity = request.quantity > 0 ? request.quantity : 1,
                Unit = request.unit ?? "PCS",
                DeliveryChallanNo = request.deliveryChallanNo,
                Image = request.transferPhoto,
                TransferDate = DateTime.UtcNow,
                Status = Domain.Enums.TransferStatus.Pending,
                RequestedByUserId = currentUserId,
                Remarks = "Central Store to Site Dispatch"
            };

            await unitOfWork.Repository<Domain.Entities.AssetTransfer>().AddAsync(transfer, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, message = "Central Store to Site transfer recorded successfully.", transferId = transfer.Id });
        }

        [HttpPost("transfer/surplus-return")]
        public async Task<IActionResult> SurplusReturnTransfer(
            [FromBody] SurplusReturnTransferDto request,
            [FromServices] Application.Interfaces.IUnitOfWork unitOfWork,
            CancellationToken cancellationToken)
        {
            var assetRepo = unitOfWork.Repository<Domain.Entities.Asset>();
            var siteRepo = unitOfWork.Repository<Domain.Entities.Site>();
            var whRepo = unitOfWork.Repository<Domain.Entities.Warehouse>();

            var asset = (await assetRepo.GetFilteredAsync(a => a.AssetNumber == request.assetCode || a.Name == request.assetName || !a.IsDeleted, cancellationToken)).FirstOrDefault();
            var firstSiteId = (await siteRepo.GetAllAsync(cancellationToken)).First().Id;

            Guid srcSiteId = (await siteRepo.GetByIdAsync(request.fromSiteId, cancellationToken)) != null ? request.fromSiteId : firstSiteId;
            Guid dstSiteId = (await siteRepo.GetByIdAsync(request.toWarehouseId, cancellationToken)) != null ? request.toWarehouseId : firstSiteId;

            var userClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid currentUserId = Guid.TryParse(userClaim, out var uG) ? uG : (await unitOfWork.Repository<Domain.Entities.User>().GetAllAsync(cancellationToken)).First().Id;

            var transfer = new Domain.Entities.AssetTransfer
            {
                AssetId = asset != null ? asset.Id : (await assetRepo.GetAllAsync(cancellationToken)).First().Id,
                ItemName = request.assetName ?? asset?.Name ?? "Surplus Cement Sacks",
                SourceSiteId = srcSiteId,
                DestinationSiteId = dstSiteId,
                Quantity = request.quantity > 0 ? request.quantity : 1,
                Unit = request.unit ?? "Sacks",
                TransferDate = DateTime.UtcNow,
                Status = Domain.Enums.TransferStatus.Pending,
                RequestedByUserId = currentUserId,
                Remarks = "Surplus Return (Site to Central Store)"
            };

            await unitOfWork.Repository<Domain.Entities.AssetTransfer>().AddAsync(transfer, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, message = "Surplus Return transfer recorded successfully.", transferId = transfer.Id });
        }

        [HttpPost("transfer/site-to-site")]
        public async Task<IActionResult> SiteToSiteTransfer(
            [FromBody] SiteToSiteTransferDto request,
            [FromServices] Application.Interfaces.IUnitOfWork unitOfWork,
            CancellationToken cancellationToken)
        {
            var assetRepo = unitOfWork.Repository<Domain.Entities.Asset>();
            var asset = (await assetRepo.GetFilteredAsync(a => a.AssetNumber == request.assetCode || a.Name == request.assetName || !a.IsDeleted, cancellationToken)).FirstOrDefault();

            var userClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid currentUserId = Guid.TryParse(userClaim, out var uG) ? uG : (await unitOfWork.Repository<Domain.Entities.User>().GetAllAsync(cancellationToken)).First().Id;

            var transfer = new Domain.Entities.AssetTransfer
            {
                AssetId = asset != null ? asset.Id : (await assetRepo.GetAllAsync(cancellationToken)).First().Id,
                ItemName = request.assetName ?? asset?.Name ?? "Steel Pipes",
                SourceSiteId = request.fromSiteId,
                DestinationSiteId = request.toSiteId,
                Quantity = request.quantity > 0 ? request.quantity : 1,
                Unit = "PCS",
                TransferDate = DateTime.UtcNow,
                Status = Domain.Enums.TransferStatus.Pending,
                RequestedByUserId = currentUserId,
                Remarks = "Direct Site to Site Transfer"
            };

            await unitOfWork.Repository<Domain.Entities.AssetTransfer>().AddAsync(transfer, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, message = "Site to Site transfer recorded successfully.", transferId = transfer.Id });
        }
    }
}
