using Grpc.Core;
using Microsoft.Extensions.Logging;
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
        internal IReadOnlyList<IGrpcSubChannel> SubChannels { get; set; } = Array.Empty<IGrpcSubChannel>();

        public Task CreateSubChannelsAsync(GrpcResolvedAddresses resolvedAddresses, string serviceName, bool isSecureConnection)
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
            _logger.LogDebug($"Start pick_first policy");
            var uriBuilder = new UriBuilder();
            uriBuilder.Host = hostsAddresses[0].Host;
            uriBuilder.Port = hostsAddresses[0].Port ?? (isSecureConnection ? 443 : 80);
            uriBuilder.Scheme = isSecureConnection ? "https" : "http";
            var uri = uriBuilder.Uri;
            var result = new List<IGrpcSubChannel> {
                _helper.CreateSubChannel(new CreateSubchannelArgs(uri, GrpcAttributes.Empty))
            };
            _logger.LogDebug($"Found a server {uri}");
            _logger.LogDebug($"SubChannels list created");
            SubChannels = result;
            _helper.UpdateBalancingState(GrpcConnectivityState.READY, new Picker(SubChannels[0]));
            return Task.CompletedTask;
        }

        public Task HandleNameResolutionErrorAsync(Status error)
        {
            // TODO
            _helper.UpdateBalancingState(GrpcConnectivityState.TRANSIENT_FAILURE, new Picker(GrpcPickResult.WithError(error)));
            return Task.CompletedTask;
        }

        public bool CanHandleEmptyAddressListFromNameResolution()
        {
            return false;
        }

        public Task RequestConnectionAsync()
        {
            //TODO
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        internal sealed class Picker : IGrpcSubChannelPicker
        {
            private readonly GrpcPickResult _pickResult;

            public Picker(GrpcPickResult pickResult)
            {
                _pickResult = pickResult ?? throw new ArgumentNullException(nameof(pickResult));
            }

            public Picker(IGrpcSubChannel subChannel)
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
