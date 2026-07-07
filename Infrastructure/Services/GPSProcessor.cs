using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class GPSProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<AssetTrackingHub> _hubContext;
        private readonly ILogger<GPSProcessor> _logger;

        public GPSProcessor(
            IServiceProvider serviceProvider,
            IHubContext<AssetTrackingHub> hubContext,
            ILogger<GPSProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GPS Processor Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    
                    var gpsHistoryRepo = unitOfWork.Repository<GPSHistory>();
                    var alertRepo = unitOfWork.Repository<Alert>();

                    var unprocessed = await gpsHistoryRepo.GetFilteredAsync(x => x.GeofenceStatus == null, stoppingToken);

                    foreach (var history in unprocessed)
                    {
                        var isInsidePuneDC = history.Latitude >= 18.4704 && history.Latitude <= 18.5704 &&
                                             history.Longitude >= 73.8067 && history.Longitude <= 73.9067;

                        history.GeofenceStatus = isInsidePuneDC ? "Inside" : "Violation";
                        gpsHistoryRepo.Update(history);

                        if (!isInsidePuneDC)
                        {
                            var device = await unitOfWork.Repository<GPSDevice>().GetByIdAsync(history.GPSDeviceId, stoppingToken);
                            var assetName = device?.Asset != null ? device.Asset.Name : "Unknown Asset";

                            var alert = new Alert
                            {
                                Id = Guid.NewGuid(),
                                AssetId = device?.AssetId,
                                AlertType = AlertType.UnauthorizedMovement,
                                Severity = AlertSeverity.Critical,
                                Title = "Geofence Violation",
                                Message = $"Asset '{assetName}' with GPS Device {device?.Imei} has exited the authorized Pune DC Geofence! Location: ({history.Latitude}, {history.Longitude})",
                                IsResolved = false,
                                CreatedOn = DateTime.UtcNow
                            };

                            await alertRepo.AddAsync(alert, stoppingToken);

                            await _hubContext.Clients.All.SendAsync("ReceiveAlertNotification", new
                            {
                                alert.Title,
                                alert.Message,
                                Severity = alert.Severity.ToString(),
                                Timestamp = alert.CreatedOn
                            }, stoppingToken);

                            _logger.LogWarning($"ALERT: Geofence violation detected for device {history.GPSDeviceId}");
                        }
                    }

                    await unitOfWork.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in GPS Processor background service.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
