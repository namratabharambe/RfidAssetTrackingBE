using Application.Interfaces;
using Infrastructure.Persistence.Context;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<AssetTrackingDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"));
            });

            services.AddScoped<IAssetRepository, AssetRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IReportService, ReportService>();

            services.AddScoped<AssetTracking.Rfid.Infrastructure.Persistence.AppDbContext>();
            services.AddScoped<AssetTracking.Rfid.ScanProcessor.ScanDataProcessorFunction>();

            services.AddHostedService<ReaderHealthMonitor>();
            services.AddHostedService<GPSProcessor>();
            services.AddHostedService<ScanProcessor>();
            services.AddHostedService<ScanDataProcessorBackgroundService>();

            return services;
        }
    }
}
