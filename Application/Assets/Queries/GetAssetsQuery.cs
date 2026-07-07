using Application.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Assets.Queries
{
    public class GetAssetsQuery : IRequest<IEnumerable<AssetDto>>
    {
    }
}
