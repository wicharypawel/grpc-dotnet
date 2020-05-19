using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

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
        private readonly IGrpcHelper _helper;

        public CdsPolicy(IGrpcHelper helper)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public ILoggerFactory LoggerFactory
        {
            set
            {
                _loggerFactory = value;
                _logger = value.CreateLogger<CdsPolicy>();
            }
        }

        internal IGrpcLoadBalancingPolicy? OverrideEdsPolicy { private get; set; }

        public void HandleNameResolutionError(Status error)
        {
            // TODO
        }

        public bool CanHandleEmptyAddressListFromNameResolution()
        {
            return true;
        }

        public void RequestConnection()
        {
        }

        internal bool Disposed { get; private set; }

        public void HandleResolvedAddresses(GrpcResolvedAddresses resolvedAddresses, string serviceName, bool isSecureConnection)
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
            if (hostsAddresses.Count != 0)
            {
                // Note that the xds resolver will return an empty list of addresses, because in the xDS API flow, 
                // the addresses are not returned until the ClusterLoadAssignment resource is obtained later.
                throw new ArgumentException($"{nameof(resolvedAddresses.HostsAddresses)} is expected to be empty.");
            }
            _xdsClientPool = resolvedAddresses.Attributes.Get(XdsAttributesConstants.XdsClientPoolInstance) as XdsClientObjectPool
                ?? throw new InvalidOperationException("Can not find xds client pool.");
            _xdsClient = _xdsClientPool.GetObject();
            var clusterName = resolvedAddresses.Attributes.Get(XdsAttributesConstants.CdsClusterName) as string
                ?? throw new InvalidOperationException("Can not find CDS cluster name.");
            _logger.LogDebug($"Start CDS policy");
            var clustersUpdate = _xdsClient.GetCdsAsync(clusterName, serviceName).Result;
            var registry = GrpcLoadBalancingPolicyRegistry.GetDefaultRegistry(_loggerFactory);
            var edsPolicyProvider = registry.GetProvider(clustersUpdate.LbPolicy);
            _edsPolicy = OverrideEdsPolicy ?? edsPolicyProvider!.CreateLoadBalancingPolicy(_helper);
            _edsPolicy.LoggerFactory = _loggerFactory;
            var resolvedAddressesNewAttributes = new GrpcResolvedAddresses(resolvedAddresses.HostsAddresses, resolvedAddresses.ServiceConfig,
                resolvedAddresses.Attributes.Add(XdsAttributesConstants.EdsClusterName, clustersUpdate.EdsServiceName ?? clusterName)); 
            _logger.LogDebug($"CDS create EDS");
            _edsPolicy.HandleResolvedAddresses(resolvedAddressesNewAttributes, serviceName, isSecureConnection);
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
            _edsPolicy?.Dispose();
            Disposed = true;
        }
    }
}
