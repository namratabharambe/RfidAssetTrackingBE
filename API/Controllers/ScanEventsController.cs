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
        public async Task<ActionResult<IEnumerable<ScanEventDto>>> GetAll(
            [FromQuery] Guid? siteId = null,
            [FromQuery] Guid? warehouseId = null,
            CancellationToken cancellationToken = default)
        {
            Guid? targetSiteId = siteId;
            Guid? targetWhId = warehouseId;

            if (Request.Headers.TryGetValue("X-Site-Id", out var hSite) && Guid.TryParse(hSite.FirstOrDefault(), out var parsedHSite) && parsedHSite != Guid.Empty)
                targetSiteId = parsedHSite;

            if (Request.Headers.TryGetValue("X-Warehouse-Id", out var hWh) && Guid.TryParse(hWh.FirstOrDefault(), out var parsedHWh) && parsedHWh != Guid.Empty)
                targetWhId = parsedHWh;

            if (!targetSiteId.HasValue && HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var claim = HttpContext.User.Claims
                    .Where(c => c.Type == "siteId" || c.Type == "sites" || c.Type == "site_id" || c.Type == "allowed_site_ids")
                    .Select(c => c.Value)
                    .FirstOrDefault(v => Guid.TryParse(v, out var parsed) && parsed != Guid.Empty);
                targetSiteId = Guid.TryParse(claim, out var g) ? g : null;
            }

            var events = await _unitOfWork.Repository<ScanEvent>().GetFilteredAsync(
                x => (!targetSiteId.HasValue || (x.Reader != null && x.Reader.SiteId == targetSiteId.Value)), 
                cancellationToken, 
                x => x.Reader, 
                x => x.HandheldDevice);
            
            var sorted = events.OrderByDescending(x => x.Timestamp).ToList();
            return Ok(_mapper.Map<List<ScanEventDto>>(sorted));
        }
    }
}
