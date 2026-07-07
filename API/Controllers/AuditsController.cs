using Application.DTOs;
using Application.Audits.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/audits")]
    public class AuditsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuditsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryAuditDto>>> GetAll()
        {
            var audits = await _mediator.Send(new GetAuditsQuery());
            return Ok(audits);
        }
    }
}
