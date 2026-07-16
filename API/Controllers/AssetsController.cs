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
        public async Task<IEnumerable<AssetDto>> Get()
        {
            return await _mediator.Send(new GetAssetsQuery());
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
