using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;

namespace Application.Audits.Queries
{
    public class GetAuditsQueryHandler : IRequestHandler<GetAuditsQuery, IEnumerable<InventoryAuditDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAuditsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<InventoryAuditDto>> Handle(GetAuditsQuery request, CancellationToken cancellationToken)
        {
            var audits = await _unitOfWork.Repository<InventoryAudit>().GetAllAsync(cancellationToken);
            
            foreach (var audit in audits)
            {
                var user = await _unitOfWork.Repository<User>().GetByIdAsync(audit.AuditorUserId, cancellationToken);
                if (user != null)
                {
                    audit.AuditorUser = user;
                }
                
                var items = await _unitOfWork.Repository<InventoryAuditItem>().GetFilteredAsync(
                    x => x.InventoryAuditId == audit.Id,
                    cancellationToken);
                audit.AuditItems = items.ToList();
            }

            return _mapper.Map<List<InventoryAuditDto>>(audits);
        }
    }
}
