using Application.Assets.Commands.CreateAsset;
using Application.Assets.Commands.DeleteAsset;
using Application.Assets.Commands.UpdateAsset;
using Application.Assets.Queries;
using Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
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
            var assets = await _mediator.Send(new GetAssetsQuery());

            // Check authenticated user claims
            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var isGlobalSuperAdmin = HttpContext.User.IsInRole("Super Admin") 
                                      || HttpContext.User.HasClaim(c => c.Type == "allowed_site_ids" && c.Value == "ALL")
                                      || HttpContext.User.HasClaim(c => c.Type == "sites" && c.Value == "GLOBAL_ALL_SITES");

                // Extract site ID assigned to user in token claim ('siteId', 'sites', 'site_id', 'allowed_site_ids')
                var tokenSiteGuid = HttpContext.User.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                    .FirstOrDefault(g => g.HasValue);

                // Extract warehouse ID assigned to user in token claim ('warehouseId', 'warehouses', 'warehouse_id')
                var tokenWhGuid = HttpContext.User.Claims
                    .Where(c => c.Type == "warehouseId" || c.Type == "warehouses" || c.Type == "warehouse_id")
                    .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                    .FirstOrDefault(g => g.HasValue);

                // Strict Site/Warehouse Scoping: Direct query parameter or token claim
                var targetSite = siteId ?? tokenSiteGuid;
                var targetWh = warehouseId ?? tokenWhGuid;

                if (targetWh.HasValue)
                {
                    assets = assets.Where(a => a.WarehouseId == targetWh.Value);
                }
                else if (targetSite.HasValue)
                {
                    assets = assets.Where(a => a.SiteId == targetSite.Value);
                }
            }
            else
            {
                if (siteId.HasValue)
                {
                    assets = assets.Where(a => a.SiteId == siteId.Value);
                }
                if (warehouseId.HasValue)
                {
                    assets = assets.Where(a => a.WarehouseId == warehouseId.Value);
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
        public async Task<Guid> Create(CreateAssetCommand command)
        {
            return await _mediator.Send(command);
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> BulkCreate([FromBody] IEnumerable<CreateAssetCommand> commands)
        {
            var createdIds = new List<Guid>();
            foreach (var command in commands)
            {
                var id = await _mediator.Send(command);
                createdIds.Add(id);
            }
            return Ok(new { count = createdIds.Count, ids = createdIds });
        }

        [HttpPost("import-file")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportFile(
            [FromForm] Microsoft.AspNetCore.Http.IFormFile file,
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
            UpdateAssetCommand command)
        {
            if (id != command.Id)
                return BadRequest();

            await _mediator.Send(command);

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _mediator.Send(
                new DeleteAssetCommand(id));

            return NoContent();
        }
    }
}
