using Application.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Assets.Queries
{
    public sealed record GetAssetByIdQuery(Guid Id)
     : IRequest<AssetDto>;
}
