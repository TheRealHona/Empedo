﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Empedo.Discord.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TempusApi;

namespace Empedo.Discord.Services
{
    public class LambdaHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<LambdaHostedService> _logger;
        private readonly TempusEmbedService _tempusEmbedService;
        private readonly IConfiguration _configuration;
        private readonly DiscordClient _discordClient;
        private readonly Tempus _tempus;
        private Timer _timer;

        public LambdaHostedService(ILogger<LambdaHostedService> logger, TempusEmbedService tempusEmbedService, IConfiguration configuration, DiscordClient discordClient, Tempus tempus)
        {
            _logger = logger;
            _tempusEmbedService = tempusEmbedService;
            _configuration = configuration;
            _discordClient = discordClient;
            _tempus = tempus;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _discordClient.GuildDownloadCompleted += InitializeTimer;

            return Task.CompletedTask;
        }

        private Task InitializeTimer(DiscordClient discordClient, GuildDownloadCompletedEventArgs e)
        {
            _timer = new Timer(TickAsync, null, TimeSpan.Zero, 
                TimeSpan.FromMinutes(5));
            
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }
        
        private async void TickAsync(object state)
        {
            _logger.LogInformation("Updating...");
            var tasks = new List<Task>
            {
                UpdateOverviewsAsync()
            };

            await Task.WhenAll(tasks);
        }

        private async Task WipeChannelAsync(DiscordChannel discordChannel)
        {
            var messages = await discordChannel.GetMessagesAsync();

            if (!messages.Any())
            {
                return;
            }
            
            await discordChannel.DeleteMessagesAsync(messages);
        }

        private async Task<DiscordChannel> GetAndWipeChannelAsync(string configurationPath)
        {
            var channel = await _discordClient.GetChannelAsync(ulong.Parse(_configuration[configurationPath]));

            await WipeChannelAsync(channel);

            return channel;
        }

        private async Task UpdateOverviewsAsync()
        {
            var channel = await _discordClient.GetChannelAsync(ulong.Parse(_configuration["Lambda:OverviewsChannelId"]));

            var servers = await _tempus.GetServerStatusAsync();
            
            var serverOverviewEmbeds = await _tempusEmbedService.GetServerOverviewAsync(servers);
            var topPlayerOnlineEmbeds = await _tempusEmbedService.GetTopPlayersOnlineAsync(servers);
            
            await WipeChannelAsync(channel);
            
            await serverOverviewEmbeds.SendAll(channel);
            await topPlayerOnlineEmbeds.SendAll(channel);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}