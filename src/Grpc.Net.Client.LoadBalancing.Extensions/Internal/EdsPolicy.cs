using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// The load balancing policy creates a subchannel to each server address.
    /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
    /// 
    /// Official name of this policy is "xDS". It is a implementation of an xDS protocol.
    /// This class implements a EDS part of the xDS.
    /// More: https://github.com/grpc/proposal/blob/master/A27-xds-global-load-balancing.md
    /// </summary>
    internal sealed class EdsPolicy : IGrpcLoadBalancingPolicy
    {
        private bool _isSecureConnection = false;
        private ILogger _logger = NullLogger.Instance;
        private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
        private XdsClientObjectPool? _xdsClientPool;
        private IXdsClient? _xdsClient;
        private readonly IGrpcHelper _helper;

        public EdsPolicy(IGrpcHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public ILoggerFactory LoggerFactory
        {
            set
            {
                _loggerFactory = value;
                _logger = value.CreateLogger<EdsPolicy>();
            }
        }
        internal bool Disposed { get; private set; }

        public async Task CreateSubChannelsAsync(GrpcNameResolutionResult resolutionResult, string serviceName, bool isSecureConnection)
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
            if (hostsAddresses.Count != 0)
            {
                // Note that the xds resolver will return an empty list of addresses, because in the xDS API flow, 
                // the addresses are not returned until the ClusterLoadAssignment resource is obtained later.
                throw new ArgumentException($"{nameof(resolutionResult)} is expected to be empty.");
            }
            _xdsClientPool = resolutionResult.Attributes.Get(XdsAttributesConstants.XdsClientPoolInstance) as XdsClientObjectPool
                ?? throw new InvalidOperationException("Can not find xds client pool.");
            _xdsClient = _xdsClientPool.GetObject();
            var edsClusterName = resolutionResult.Attributes.Get(XdsAttributesConstants.EdsClusterName) as string
                ?? throw new InvalidOperationException("Can not find EDS cluster name.");
            _isSecureConnection = isSecureConnection;
            _logger.LogDebug($"Start EDS policy");
            var endpointUpdate = await _xdsClient.GetEdsAsync(edsClusterName).ConfigureAwait(false);
            var localities = endpointUpdate.LocalityLbEndpoints.Values.ToList();
            var childPolicies = localities.Select(locality =>
            {
                var serverAddressList = locality.Endpoints.Select(x => x.HostsAddresses).SelectMany(x => x);
                var childPicker = new RoundRobinPicker(GrpcHostAddressListToGrcpSubChannel(serverAddressList));
                return new WeightedRandomPicker.WeightedChildPicker(locality.LocalityWeight, childPicker);
            }).ToList();
            _helper.UpdateBalancingState(GrpcConnectivityState.READY, new WeightedRandomPicker(childPolicies));
            _logger.LogDebug($"SubChannels list created");
        }

        public Task HandleNameResolutionErrorAsync(Status error)
        {
            // TODO
            return Task.CompletedTask;
        }

        public bool CanHandleEmptyAddressListFromNameResolution()
        {
            return true;
        }

        public Task RequestConnectionAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }
            if (_xdsClient != null)
            {
                _xdsClientPool?.ReturnObject(_xdsClient); // _xdsClientPool is responsible for calling Dispose on _xdsClient
                _xdsClient = null; // object returned to the pool should not be used
            }
            Disposed = true;
        }

        private List<IGrpcSubChannel> GrpcHostAddressListToGrcpSubChannel(IEnumerable<GrpcHostAddress> serverList)
        {
            _logger.LogDebug($"xds received server list for locality");
            var result = new List<IGrpcSubChannel>();
            foreach (var server in serverList)
            {
                var uriBuilder = new UriBuilder();
                uriBuilder.Host = server.Host;
                uriBuilder.Port = server.Port ?? (_isSecureConnection ? 443 : 80);
                uriBuilder.Scheme = _isSecureConnection ? "https" : "http";
                var uri = uriBuilder.Uri;
                result.Add(_helper.CreateSubChannel(new CreateSubchannelArgs(uri, GrpcAttributes.Empty)));
                _logger.LogDebug($"Found a server {uri}");
            }
            return result;
        }
    }
}
