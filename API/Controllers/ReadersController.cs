using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/readers")]
    public class ReadersController : CrudControllerBase<Reader, ReaderDto, CreateReaderDto>
    {
        public ReadersController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper)
        {
        }

        [AllowAnonymous]
        [HttpGet]
        public override async Task<ActionResult<IEnumerable<ReaderDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 200,
            [FromQuery] string? search = null,
            CancellationToken cancellationToken = default)
        {
            var repo = UnitOfWork.Repository<Reader>();
            var (items, total) = await repo.GetPagedAsync(
                page,
                size,
                search,
                null,
                null,
                cancellationToken);
            Response.Headers.Add("X-Total-Count", total.ToString());
            return Ok(Mapper.Map<List<ReaderDto>>(items));
        }
    }
}
