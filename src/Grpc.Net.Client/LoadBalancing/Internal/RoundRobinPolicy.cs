using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// The load balancing policy creates a subchannel to each server address.
    /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
    /// 
    /// Official name of this policy is "round_robin". It is a implementation of an balancing-aware client.
    /// More: https://github.com/grpc/grpc/blob/master/doc/load-balancing.md#balancing-aware-client
    /// </summary>
    internal sealed class RoundRobinPolicy : IGrpcLoadBalancingPolicy
    {
        private ILogger _logger = NullLogger.Instance;
        private readonly IGrpcHelper _helper;

        public RoundRobinPolicy(IGrpcHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<RoundRobinPolicy>();
        }
        internal IReadOnlyList<IGrpcSubChannel> SubChannels { get; set; } = Array.Empty<IGrpcSubChannel>();

        public Task HandleResolvedAddressesAsync(GrpcResolvedAddresses resolvedAddresses, string serviceName, bool isSecureConnection)
        {
            if (resolvedAddresses == null)
            {
                throw new ArgumentNullException(nameof(resolvedAddresses));
            }
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException($"{nameof(serviceName)} not defined.");
            }
            var hostsAddresses = resolvedAddresses.HostsAddresses;
            hostsAddresses = hostsAddresses.Where(x => !x.IsLoadBalancer).ToList();
            if (hostsAddresses.Count == 0)
            {
                throw new ArgumentException($"{nameof(resolvedAddresses)} must contain at least one non-blancer address.");
            }
            _logger.LogDebug($"Start round_robin policy");
            var result = hostsAddresses.Select(x =>
            {
                var uriBuilder = new UriBuilder();
                uriBuilder.Host = x.Host;
                uriBuilder.Port = x.Port ?? (isSecureConnection ? 443 : 80);
                uriBuilder.Scheme = isSecureConnection ? "https" : "http";
                var uri = uriBuilder.Uri;
                _logger.LogDebug($"Found a server {uri}");
                return _helper.CreateSubChannel(new CreateSubchannelArgs(uri, GrpcAttributes.Empty));
            }).ToList();
            _logger.LogDebug($"SubChannels list created");
            SubChannels = result;
            _helper.UpdateBalancingState(GrpcConnectivityState.READY, new ReadyPicker(SubChannels));
            return Task.CompletedTask;
        }

        public Task HandleNameResolutionErrorAsync(Status error)
        {
            // TODO
            _helper.UpdateBalancingState(GrpcConnectivityState.TRANSIENT_FAILURE, new EmptyPicker(error));
            return Task.CompletedTask;
        }

        public bool CanHandleEmptyAddressListFromNameResolution()
        {
            return false;
        }

        public Task RequestConnectionAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        internal sealed class ReadyPicker : IGrpcSubChannelPicker
        {
            private readonly IReadOnlyList<IGrpcSubChannel> _subChannels;
            private int _subChannelsSelectionCounter = -1;

            public ReadyPicker(IReadOnlyList<IGrpcSubChannel> subChannels)
            {
                _subChannels = subChannels ?? throw new ArgumentNullException(nameof(subChannels));
            }

            public GrpcPickResult GetNextSubChannel()
            {
                var nextSubChannel = _subChannels[Interlocked.Increment(ref _subChannelsSelectionCounter) % _subChannels.Count];
                return GrpcPickResult.WithSubChannel(nextSubChannel);
            }

            public void Dispose()
            {
            }
        }

        internal sealed class EmptyPicker : IGrpcSubChannelPicker
        {
            private readonly Status _status;

            public EmptyPicker(Status status)
            {
                _status = status;
            }

            public GrpcPickResult GetNextSubChannel()
            {
                return _status.StatusCode == StatusCode.OK ? GrpcPickResult.WithNoResult() : GrpcPickResult.WithError(_status);
            }

            public void Dispose()
            {
            }
        }
    }
}
