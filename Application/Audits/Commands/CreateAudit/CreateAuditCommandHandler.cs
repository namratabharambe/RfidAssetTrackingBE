using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Audits.Commands.CreateAudit
{
    public class CreateAuditCommandHandler : IRequestHandler<CreateAuditCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateAuditCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(CreateAuditCommand request, CancellationToken cancellationToken)
        {
            var audit = new InventoryAudit
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                AuditDate = DateTime.UtcNow,
                Status = AuditStatus.Scheduled,
                AuditorUserId = request.AuditorUserId,
                CreatedOn = DateTime.UtcNow
            };

            await _unitOfWork.Repository<InventoryAudit>().AddAsync(audit, cancellationToken);

            // Fetch assets for auditing
            var assetRepo = _unitOfWork.Repository<Asset>();
            var assets = request.LocationId.HasValue
                ? await assetRepo.GetFilteredAsync(x => x.SiteId == request.LocationId.Value, cancellationToken) // match siteId/locationId
                : await assetRepo.GetAllAsync(cancellationToken);

            var auditItemRepo = _unitOfWork.Repository<InventoryAuditItem>();
            foreach (var asset in assets)
            {
                var auditItem = new InventoryAuditItem
                {
                    Id = Guid.NewGuid(),
                    InventoryAuditId = audit.Id,
                    AssetId = asset.Id,
                    ExpectedLocationId = asset.LocationId, // Using LocationId as the expected location/bin
                    Status = AuditItemStatus.Missing,
                    CreatedOn = DateTime.UtcNow
                };
                await auditItemRepo.AddAsync(auditItem, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return audit.Id;
        }
    }
}
