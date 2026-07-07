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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ScanEventDto>>> GetAll(CancellationToken cancellationToken)
        {
            var events = await _unitOfWork.Repository<ScanEvent>().GetFilteredAsync(
                x => true, 
                cancellationToken, 
                x => x.Reader, 
                x => x.HandheldDevice);
            
            var sorted = events.OrderByDescending(x => x.Timestamp).ToList();
            return Ok(_mapper.Map<List<ScanEventDto>>(sorted));
        }
    }
}
