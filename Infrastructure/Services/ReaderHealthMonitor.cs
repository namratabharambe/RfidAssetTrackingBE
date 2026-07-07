using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ReaderHealthMonitor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<AssetTrackingHub> _hubContext;
        private readonly ILogger<ReaderHealthMonitor> _logger;

        public ReaderHealthMonitor(
            IServiceProvider serviceProvider,
            IHubContext<AssetTrackingHub> hubContext,
            ILogger<ReaderHealthMonitor> logger)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Reader Health Monitor Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var readerRepository = unitOfWork.Repository<Reader>();

                    var readers = await readerRepository.GetAllAsync(stoppingToken);

                    foreach (var reader in readers)
                    {
                        var isOnline = false;
                        try
                        {
                            using var ping = new Ping();
                            var reply = await ping.SendPingAsync(reader.IpAddress, 1000);
                            isOnline = reply.Status == IPStatus.Success;
                        }
                        catch
                        {
                            isOnline = false;
                        }

                        var newStatus = isOnline ? DeviceStatus.Online : DeviceStatus.Offline;

                        if (reader.Status != newStatus)
                        {
                            reader.Status = newStatus;
                            readerRepository.Update(reader);
                            
                            await _hubContext.Clients.All.SendAsync("ReceiveReaderStatus", reader.Name, newStatus.ToString(), stoppingToken);
                            _logger.LogWarning($"Reader {reader.Name} status changed to {newStatus}");
                        }
                    }

                    await unitOfWork.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in Reader Health Monitor background service.");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
