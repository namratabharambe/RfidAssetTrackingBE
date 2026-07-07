using Application.Interfaces;
using Domain.Entities;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.GPS.Queries.GetGpsAndroidLocation
{
    public class GetGpsAndroidLocationQueryHandler : IRequestHandler<GetGpsAndroidLocationQuery, object>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetGpsAndroidLocationQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<object> Handle(GetGpsAndroidLocationQuery request, CancellationToken cancellationToken)
        {
            var vehicles = await _unitOfWork.Repository<Vehicle>().GetFilteredAsync(
                v => v.DeviceNum == request.DeviceNum,
                cancellationToken);
                
            var vehicle = vehicles.FirstOrDefault();
            if (vehicle == null)
            {
                return new
                {
                    cmd = "getVehiclesLocation",
                    result = 0,
                    resultNote = $"Vehicle with ID {request.DeviceNum} not found.",
                    detail = new { data = Array.Empty<object>(), lastTime = 0 }
                };
            }

            bool isOnline = (DateTime.UtcNow - vehicle.UpdateTime).TotalMinutes < 10;
            string statusStr = isOnline ? "Online" : "Offline";
            var data = new
            {
                VehicleID = vehicle.VehicleID,
                RegName = vehicle.RegName,
                DeviceNum = vehicle.DeviceNum,
                DeviceType = 61,
                MonitorType = 0,
                ModelType = 0,
                Vin = "",
                ICCID = "",
                VelStatus = isOnline ? 1 : 0,
                EngineNo = "",
                SimNum = "",
                InstallTime = 1770566400000,
                ExpirationTime = 2086099200000,
                VelPassword = "xxxxx",
                Remark = "",
                GpsTime = new DateTimeOffset(vehicle.GpsTime).ToUnixTimeMilliseconds(),
                Lat = vehicle.Lat.ToString("F6"),
                Lon = vehicle.Lon.ToString("F6"),
                OnlineStatus = statusStr,
                RunStatus = statusStr,
                OffsetLat = vehicle.Lat.ToString("F6"),
                OffsetLon = vehicle.Lon.ToString("F6"),
                Odometer = "0.0",
                PlaceName = "",
                Location = "",
                Speed = vehicle.Speed.ToString("F0"),
                Direction = vehicle.Direction.ToString("F0"),
                AlarmStatus = "",
                Battery = (vehicle.Battery / 100.0).ToString("F2"),
                Status = vehicle.Status,
                LoStatus = 0,
                IsBattery = 0,
                OnlineType = 3,
                MonitorTypeName = "",
                UpdateTime = new DateTimeOffset(vehicle.UpdateTime).ToUnixTimeMilliseconds(),
                GpsStatus = 2,
                IsOnline = isOnline ? 1 : 0,
                IsTemperature = false,
                IsPowerBattery = false,
                T1 = "",
                T2 = "",
                T3 = "",
                T4 = "",
                OnlineStatusStr = statusStr,
                IsTyre = 0,
                H1 = "",
                H2 = "",
                H3 = "",
                H4 = "",
                F1 = "",
                F2 = ""
            };

            return new
            {
                cmd = "getVehiclesLocation",
                result = 1,
                resultNote = "Success",
                detail = new
                {
                    data = new[] { data },
                    lastTime = 0
                }
            };
        }
    }
}
