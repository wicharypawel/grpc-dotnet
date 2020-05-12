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
        private int _subChannelsSelectionCounter = -1;
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
        internal IReadOnlyList<GrpcSubChannel> SubChannels { get; set; } = Array.Empty<GrpcSubChannel>();
        internal IReadOnlyList<GrpcPickResult> PickResults { get; set; } = Array.Empty<GrpcPickResult>();

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
            _logger.LogDebug($"Start round_robin policy");
            var result = hostsAddresses.Select(x =>
            {
                var uriBuilder = new UriBuilder();
                uriBuilder.Host = x.Host;
                uriBuilder.Port = x.Port ?? (isSecureConnection ? 443 : 80);
                uriBuilder.Scheme = isSecureConnection ? "https" : "http";
                var uri = uriBuilder.Uri;
                _logger.LogDebug($"Found a server {uri}");
                return new GrpcSubChannel(uri);
            }).ToList();
            _logger.LogDebug($"SubChannels list created");
            SubChannels = result;
            PickResults = result.Select(x => GrpcPickResult.WithSubChannel(x)).ToArray();
            return Task.CompletedTask;
        }

        public GrpcPickResult GetNextSubChannel()
        {
            return PickResults[Interlocked.Increment(ref _subChannelsSelectionCounter) % PickResults.Count];
        }

        public void Dispose()
        {
        }
    }
}
