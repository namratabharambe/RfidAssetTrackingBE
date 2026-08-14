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

                // Extract all site IDs assigned to user in token claims ('sites', 'siteId', 'allowed_site_ids')
                var tokenSiteGuids = HttpContext.User.Claims
                    .Where(c => c.Type == "sites" || c.Type == "siteId" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .Distinct()
                    .ToList();

                // Extract all warehouse IDs assigned to user in token claims ('warehouses', 'warehouseId', 'allowed_warehouse_ids')
                var tokenWhGuids = HttpContext.User.Claims
                    .Where(c => c.Type == "warehouses" || c.Type == "warehouseId" || c.Type == "warehouse_id" || c.Type == "allowed_warehouse_ids")
                    .Select(c => Guid.TryParse(c.Value, out var g) ? (Guid?)g : null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .Distinct()
                    .ToList();

                // Strict Site Scoping: Direct query parameter or token site claims
                if (siteId.HasValue)
                {
                    assets = assets.Where(a => a.SiteId == siteId.Value);
                }
                else if (!isGlobalSuperAdmin && tokenSiteGuids.Any())
                {
                    assets = assets.Where(a => a.SiteId.HasValue && tokenSiteGuids.Contains(a.SiteId.Value));
                }

                // Strict Warehouse Scoping: Direct query parameter or token warehouse claims
                if (warehouseId.HasValue)
                {
                    assets = assets.Where(a => a.WarehouseId == warehouseId.Value || a.WarehouseId == null);
                }
                else if (!isGlobalSuperAdmin && tokenWhGuids.Any())
                {
                    assets = assets.Where(a => a.WarehouseId == null || (a.WarehouseId.HasValue && tokenWhGuids.Contains(a.WarehouseId.Value)));
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
