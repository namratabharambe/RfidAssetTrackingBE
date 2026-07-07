using MediatR;

namespace Application.GPS.Queries.GetGpsAndroidLocation
{
    public record GetGpsAndroidLocationQuery(string DeviceNum) : IRequest<object>;
}
