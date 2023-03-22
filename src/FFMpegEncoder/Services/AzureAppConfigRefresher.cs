using FFMpegCore.Pipes;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Extensions;
using FFMpegCore.Enums;
using Microsoft.Extensions.Options;
using FFMpegEncoder.Models;
using System.Threading.Channels;
using System.Xml.Linq;
using System.Linq;
using System.Runtime;
using System.Runtime.Intrinsics.X86;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using FFMpegCore.Builders.MetaData;
using Instances;
using FFMpegCore.Exceptions;
using Microsoft.VisualBasic;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using System.Threading;

namespace FFMpegEncoder.Services
{
    public class AzureAppConfigRefresher : BackgroundService, IDisposable
    {
        private readonly ILogger<AzureAppConfigRefresher> _logger;
        private readonly IConfigurationRefresher _refresher;

        private PeriodicTimer _timer;
        public AzureAppConfigRefresher(ILogger<AzureAppConfigRefresher> logger, IConfigurationRefresher refresher)
        {
            _logger = logger;
            _refresher = refresher;
            _timer = new PeriodicTimer(
                TimeSpan.FromMinutes(5));
        }

        public override void Dispose()
        {
            _timer?.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (await _timer.WaitForNextTickAsync(stoppingToken))
            {
                if (await _refresher.TryRefreshAsync(stoppingToken))
                {
                    _logger.LogInformation($" {_refresher.AppConfigurationEndpoint} successfully refreshed");
                } 
                else
                {
                    _logger.LogWarning($" {_refresher.AppConfigurationEndpoint} failed refreshed");
                }        
            }

            _logger.LogWarning($"AzureAppConfigRefresher: stop");
        }
    }
}