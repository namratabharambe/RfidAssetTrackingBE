using Application.DTOs;
using Application.Audits.Queries;
using Application.Audits.Commands.CreateAudit;
using Application.Audits.Commands.ReconcileAudit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
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

        [HttpPost]
        public async Task<ActionResult<Guid>> Create([FromBody] CreateAuditCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [HttpPost("{id:guid}/reconcile")]
        public async Task<ActionResult<bool>> Reconcile(Guid id, [FromBody] ReconcileRequest request)
        {
            var command = new ReconcileAuditCommand(id, request.ScannedEpcs, request.ScannedLocationId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }

    public class ReconcileRequest
    {
        public List<string> ScannedEpcs { get; set; } = new();
        public Guid? ScannedLocationId { get; set; }
    }
}
