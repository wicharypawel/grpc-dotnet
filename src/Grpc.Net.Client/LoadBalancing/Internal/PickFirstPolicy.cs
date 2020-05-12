﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class PickFirstPolicy : IGrpcLoadBalancingPolicy
    {
        private ILogger _logger = NullLogger.Instance;
        private readonly IGrpcHelper _helper;

        public PickFirstPolicy(IGrpcHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }
        
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<PickFirstPolicy>();
        }
        internal IReadOnlyList<GrpcSubChannel> SubChannels { get; set; } = Array.Empty<GrpcSubChannel>();

        public Task CreateSubChannelsAsync(GrpcNameResolutionResult resolutionResult, string serviceName, bool isSecureConnection)
        {
            if (resolutionResult == null)
            {
                throw new ArgumentNullException(nameof(resolutionResult));
            }
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException($"{nameof(serviceName)} not defined.");
            }
            var hostsAddresses = resolutionResult.HostsAddresses;
            hostsAddresses = hostsAddresses.Where(x => !x.IsLoadBalancer).ToList();
            if (hostsAddresses.Count == 0)
            {
                throw new ArgumentException($"{nameof(resolutionResult)} must contain at least one non-blancer address.");
            }
            _logger.LogDebug($"Start pick_first policy");
            var uriBuilder = new UriBuilder();
            uriBuilder.Host = hostsAddresses[0].Host;
            uriBuilder.Port = hostsAddresses[0].Port ?? (isSecureConnection ? 443 : 80);
            uriBuilder.Scheme = isSecureConnection ? "https" : "http";
            var uri = uriBuilder.Uri;
            var result = new List<GrpcSubChannel> {
                new GrpcSubChannel(uri)
            };
            _logger.LogDebug($"Found a server {uri}");
            _logger.LogDebug($"SubChannels list created");
            SubChannels = result;
            _helper.UpdateBalancingState(GrpcConnectivityState.READY, new Picker(SubChannels[0]));
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        internal sealed class Picker : IGrpcSubChannelPicker
        {
            private readonly GrpcPickResult _pickResult;

            public Picker(GrpcSubChannel subChannel)
            {
                _pickResult = GrpcPickResult.WithSubChannel(subChannel) ?? throw new ArgumentNullException(nameof(subChannel));
            }

            public GrpcPickResult GetNextSubChannel()
            {
                return _pickResult;
            }

            public void Dispose()
            {
            }
        }
    }
}
