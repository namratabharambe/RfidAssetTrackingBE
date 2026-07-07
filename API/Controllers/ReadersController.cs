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
    }
}
