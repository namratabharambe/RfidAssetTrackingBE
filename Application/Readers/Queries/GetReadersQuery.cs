using Application.DTOs;
using MediatR;
using System.Collections.Generic;

namespace Application.Readers.Queries
{
    public record GetReadersQuery : IRequest<IEnumerable<ReaderDto>>;
}
