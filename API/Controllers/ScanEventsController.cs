using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/scanevents")]
    public class ScanEventsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ScanEventsController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ScanEventDto>>> GetAll(CancellationToken cancellationToken)
        {
            var claim = HttpContext.User.Claims
                .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                .Select(c => c.Value)
                .FirstOrDefault(v => Guid.TryParse(v, out _));

            Guid? targetSiteId = Guid.TryParse(claim, out var g) ? g : null;

            var events = await _unitOfWork.Repository<ScanEvent>().GetFilteredAsync(
                x => !targetSiteId.HasValue || (x.Reader != null && x.Reader.SiteId == targetSiteId.Value), 
                cancellationToken, 
                x => x.Reader, 
                x => x.HandheldDevice);
            
            var sorted = events.OrderByDescending(x => x.Timestamp).ToList();
            return Ok(_mapper.Map<List<ScanEventDto>>(sorted));
        }
    }
}
