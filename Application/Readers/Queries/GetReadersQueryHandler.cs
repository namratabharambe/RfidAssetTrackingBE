using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;

namespace Application.Readers.Queries
{
    public class GetReadersQueryHandler : IRequestHandler<GetReadersQuery, IEnumerable<ReaderDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetReadersQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ReaderDto>> Handle(GetReadersQuery request, CancellationToken cancellationToken)
        {
            var readers = await _unitOfWork.Repository<Reader>().GetAllAsync(cancellationToken);
            
            foreach (var reader in readers)
            {
                var site = await _unitOfWork.Repository<Site>().GetByIdAsync(reader.SiteId, cancellationToken);
                if (site != null)
                {
                    reader.Site = site;
                }
            }

            return _mapper.Map<List<ReaderDto>>(readers);
        }
    }
}
