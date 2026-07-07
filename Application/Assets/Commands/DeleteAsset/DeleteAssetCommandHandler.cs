using Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Assets.Commands.DeleteAsset
{
    public class DeleteAssetCommandHandler
      : IRequestHandler<DeleteAssetCommand>
    {
        private readonly IAssetRepository _assetRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteAssetCommandHandler(
            IAssetRepository assetRepository,
            IUnitOfWork unitOfWork)
        {
            _assetRepository = assetRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            DeleteAssetCommand request,
            CancellationToken cancellationToken)
        {
            var asset = await _assetRepository.GetByIdAsync(
                request.Id,
                cancellationToken);

            if (asset is null)
                throw new KeyNotFoundException("Asset not found.");

            _assetRepository.Delete(asset);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
