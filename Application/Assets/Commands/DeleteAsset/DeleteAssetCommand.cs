using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Assets.Commands.DeleteAsset
{
    public class DeleteAssetCommand : IRequest
    {
        public Guid Id { get; }

        public DeleteAssetCommand(Guid id)
        {
            Id = id;
        }
    }
}
