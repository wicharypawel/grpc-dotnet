﻿using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using static Grpc.Net.Client.LoadBalancing.Extensions.Internal.EnvoyProtoData;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// The load balancing policy creates a subchannel to each server address.
    /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
    /// 
    /// Official name of this policy is "xDS". It is a implementation of an xDS protocol.
    /// This class implements a CDS part of the xDS.
    /// More: https://github.com/grpc/proposal/blob/master/A27-xds-global-load-balancing.md
    /// </summary>
    internal sealed class CdsPolicy : IGrpcLoadBalancingPolicy
    {
        private ILogger _logger = NullLogger.Instance;
        private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
        private XdsClientObjectPool? _xdsClientPool;
        private IXdsClient? _xdsClient;
        private IGrpcLoadBalancingPolicy? _edsPolicy;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set
            {
                _loggerFactory = value;
                _logger = value.CreateLogger<CdsPolicy>();
            }
        }

        /// <summary>
        /// Property created for testing purposes, allows setter injection
        /// </summary>
        internal IGrpcLoadBalancingPolicy? OverrideEdsPolicy { private get; set; }

        internal bool Disposed { get; private set; }

        /// <summary>
        /// Creates a subchannel to each server address. Depending on policy this may require additional 
        /// steps eg. using xds protocol and reaching control plane to get list of servers.
        /// </summary>
        /// <param name="resolutionResult">Resolved list of servers and/or lookaside load balancers. xDS policy expect an empty list.</param>
        /// <param name="serviceName">The name of the load balanced service (e.g., service.googleapis.com).</param>
        /// <param name="isSecureConnection">Flag if connection between client and destination server should be secured.</param>
        /// <returns>List of subchannels.</returns>
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
            var clusterName = resolutionResult.Attributes.Get(XdsAttributesConstants.CdsClusterName) as string
                ?? throw new InvalidOperationException("Can not find CDS cluster name.");
            _logger.LogDebug($"Start CDS policy");
            var clustersUpdate = await _xdsClient.GetCdsAsync(clusterName, serviceName).ConfigureAwait(false);
            var registry = GrpcLoadBalancingPolicyRegistry.GetDefaultRegistry(_loggerFactory);
            var edsPolicyProvider = registry.GetProvider(clustersUpdate.LbPolicy);
            _edsPolicy = OverrideEdsPolicy ?? edsPolicyProvider!.CreateLoadBalancingPolicy();
            _edsPolicy.LoggerFactory = _loggerFactory;
            var resolutionResultNewAttributes = new GrpcNameResolutionResult(resolutionResult.HostsAddresses, resolutionResult.ServiceConfig,
                resolutionResult.Attributes.Add(XdsAttributesConstants.EdsClusterName, clustersUpdate.EdsServiceName ?? clusterName)); 
            _logger.LogDebug($"CDS create EDS");
            await _edsPolicy.CreateSubChannelsAsync(resolutionResultNewAttributes, serviceName, isSecureConnection).ConfigureAwait(false);
        }

        /// <summary>
        /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
        /// </summary>
        /// <returns>Selected subchannel.</returns>
        public GrpcSubChannel GetNextSubChannel()
        {
            return _edsPolicy!.GetNextSubChannel();
        }

        /// <summary>
        /// Releases the resources used by the <see cref="CdsPolicy"/> class.
        /// </summary>
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
            _edsPolicy?.Dispose();
            Disposed = true;
        }
    }
}
