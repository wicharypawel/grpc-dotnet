using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using Envoy.Api.V2;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// The load balancing policy creates a subchannel to each server address.
    /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
    /// 
    /// Official name of this policy is "xDS". It is a implementation of an xDS protocol.
    /// More: https://github.com/grpc/proposal/blob/master/A27-xds-global-load-balancing.md
    /// </summary>
    internal sealed class XdsPolicy : IGrpcLoadBalancingPolicy
    {
        private bool _isSecureConnection = false;
        private int _subChannelsSelectionCounter = -1;
        private ILogger _logger = NullLogger.Instance;
        private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
        //private ILoadBalancerClient? _loadBalancerClient;
        private IXdsClient _xdsClient;

        public XdsPolicy()
        {
            _xdsClient = XdsClientFactory.CreateXdsClient();
        }

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set
            {
                _loggerFactory = value;
                _logger = value.CreateLogger<XdsPolicy>();
            }
        }
        internal bool Disposed { get; private set; }
        internal IReadOnlyList<GrpcSubChannel> SubChannels { get; set; } = Array.Empty<GrpcSubChannel>();

        /// <summary>
        /// Creates a subchannel to each server address. Depending on policy this may require additional 
        /// steps eg. using xds protocol and reaching control plane to get list of servers.
        /// </summary>
        /// <param name="resolutionResult">Resolved list of servers and/or lookaside load balancers. xDS policy expect an empty list.</param>
        /// <param name="serviceName">The name of the load balanced service (e.g., service.googleapis.com).</param>
        /// <param name="isSecureConnection">Flag if connection between client and destination server should be secured.</param>
        /// <returns>List of subchannels.</returns>
        public async Task CreateSubChannelsAsync(List<GrpcNameResolutionResult> resolutionResult, string serviceName, bool isSecureConnection)
        {
            if (resolutionResult == null)
            {
                throw new ArgumentNullException(nameof(resolutionResult));
            }
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException($"{nameof(serviceName)} not defined");
            }
            if (resolutionResult.Count != 0)
            {
                // Note that the xds resolver will return an empty list of addresses, because in the xDS API flow, 
                // the addresses are not returned until the ClusterLoadAssignment resource is obtained later.
                throw new ArgumentException($"{nameof(resolutionResult)} is expected to be empty.");
            }
            _isSecureConnection = isSecureConnection;
            _logger.LogDebug($"Start xds policy");
            _logger.LogDebug($"Start connection to control plane");
            var clusters = await _xdsClient.GetCdsAsync().ConfigureAwait(false);
            var clustersForServiceName = clusters
                .Where(x => x.Type == Cluster.Types.DiscoveryType.Eds)
                .Where(x => x?.EdsClusterConfig?.ServiceName.Contains(serviceName, StringComparison.OrdinalIgnoreCase) ?? false);
            var clusterLoadAssignments = await _xdsClient.GetEdsAsync(clustersForServiceName.First().Name).ConfigureAwait(false);
            var sad = clusterLoadAssignments[0].Endpoints[0].LbEndpoints.Select(x => x.Endpoint.Address.SocketAddress);
            await UseServerListSubChannelsAsync(sad).ConfigureAwait(false);
            _logger.LogDebug($"SubChannels list created");
        }

        /// <summary>
        /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
        /// </summary>
        /// <returns>Selected subchannel.</returns>
        public GrpcSubChannel GetNextSubChannel()
        {
            return SubChannels[Interlocked.Increment(ref _subChannelsSelectionCounter) % SubChannels.Count];
        }

        /// <summary>
        /// Releases the resources used by the <see cref="XdsPolicy"/> class.
        /// </summary>
        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }
            _xdsClient?.Dispose();
            Disposed = true;
        }

        private Task UseServerListSubChannelsAsync(IEnumerable<Envoy.Api.V2.Core.SocketAddress> serverList)
        {
            _logger.LogDebug($"xds received server list");
            //TODO add cache here
            var result = new List<GrpcSubChannel>();
            foreach (var server in serverList)
            {
                var uriBuilder = new UriBuilder();
                uriBuilder.Host = server.Address;
                uriBuilder.Port = Convert.ToInt32(server.PortValue);
                uriBuilder.Scheme = _isSecureConnection ? "https" : "http";
                var uri = uriBuilder.Uri;
                result.Add(new GrpcSubChannel(uri, string.Empty));
                _logger.LogDebug($"Found a server {uri}");
            }
            SubChannels = result;
            return Task.CompletedTask;
        }
    }
}
