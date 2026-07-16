using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ScanDataProcessorBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScanDataProcessorBackgroundService> _logger;

        public ScanDataProcessorBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ScanDataProcessorBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScanDataProcessorBackgroundService is starting.");

            // Wait 5 seconds on startup before first check
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var processor = scope.ServiceProvider.GetRequiredService<AssetTracking.Rfid.ScanProcessor.ScanDataProcessorFunction>();
                        // We pass a null TimerInfo as it's not used in the function logic
                        await processor.RunAsync(null!);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing ScanDataProcessorFunction background job.");
                }

                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); // Check every 15 seconds for responsiveness
            }
        }
    }
}
