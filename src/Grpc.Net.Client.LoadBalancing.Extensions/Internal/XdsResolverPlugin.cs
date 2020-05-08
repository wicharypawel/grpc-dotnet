using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port).
    /// 
    /// Note that the xds resolver will return an empty list of addresses, because in the xDS API flow, 
    /// the addresses are not returned until the ClusterLoadAssignment resource is obtained later.
    /// 
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// More: https://github.com/grpc/proposal/blob/master/A27-xds-global-load-balancing.md
    /// </summary>
    internal sealed class XdsResolverPlugin : IGrpcResolverPlugin
    {
        private XdsResolverPluginOptions _options;
        private ILogger _logger = NullLogger.Instance;
        private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
        private XdsClientObjectPool? _xdsClientPool;
        private IXdsClient? _xdsClient;
        private readonly string _defaultLoadBalancingPolicy;
        private Uri? _target = null;
        private IGrpcNameResolutionObserver? _observer = null;
        private CancellationTokenSource? _cancellationTokenSource = null;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set
            {
                _loggerFactory = value;
                _logger = value.CreateLogger<XdsResolverPlugin>();
            }
        }

        /// <summary>
        /// Creates a <seealso cref="XdsResolverPlugin"/> using default <seealso cref="XdsResolverPluginOptions"/>.
        /// </summary>
        public XdsResolverPlugin() : this(GrpcAttributes.Empty)
        {
        }

        /// <summary>
        /// Creates a <seealso cref="XdsResolverPlugin"/> using specified <seealso cref="GrpcAttributes"/>.
        /// </summary>
        /// <param name="attributes">Attributes with options.</param>
        public XdsResolverPlugin(GrpcAttributes attributes)
        {
            var options = attributes.Get(GrpcAttributesLbConstants.XdsResolverOptions) as XdsResolverPluginOptions;
            _options = options ?? new XdsResolverPluginOptions();
            _defaultLoadBalancingPolicy = attributes.Get(GrpcAttributesConstants.DefaultLoadBalancingPolicy) as string
                ?? "pick_first";
        }

        /// <summary>
        /// Property created for testing purposes, allows setter injection
        /// </summary>
        internal XdsClientFactory? OverrideXdsClientFactory { private get; set; }

        public void Subscribe(Uri target, IGrpcNameResolutionObserver observer)
        {
            if (_observer != null)
            {
                throw new InvalidOperationException("Observer already registered.");
            }
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _cancellationTokenSource = new CancellationTokenSource();
            if (!target.Scheme.Equals("xds", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{nameof(XdsResolverPlugin)} require xds:// scheme to set as target address.");
            }
            if (_xdsClient == null)
            {
                _xdsClientPool = new XdsClientObjectPool(OverrideXdsClientFactory ?? new XdsClientFactory(_loggerFactory), _loggerFactory);
                _xdsClient = _xdsClientPool.GetObject();
            }
            _logger.LogDebug($"Start XdsResolverPlugin");
            var listenerName = $"{target.Host}:{target.Port}";
            _xdsClient.Subscribe(listenerName, new ConfigUpdateObserver(this, observer));
        }

        public void Unsubscribe()
        {
            _observer = null;
            _target = null;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        public void RefreshResolution()
        {
            // Because xDS data is transported via bidirectional ADS, it is not required to refresh
        }

        public void Dispose()
        {
            if (_xdsClient != null)
            {
                _xdsClientPool?.ReturnObject(_xdsClient); // _xdsClientPool is responsible for calling Dispose on _xdsClient
                _xdsClient = null; // object returned to the pool should not be used
            }
            Unsubscribe();
        }

        private sealed class ConfigUpdateObserver : IConfigUpdateObserver
        {
            private readonly XdsResolverPlugin _resolverPlugin;
            private readonly IGrpcNameResolutionObserver _observer;

            public ConfigUpdateObserver(XdsResolverPlugin resolverPlugin,IGrpcNameResolutionObserver observer)
            {
                _resolverPlugin = resolverPlugin;
                _observer = observer;
            }

            public void OnError(Status error)
            {
                _observer.OnError(error);
            }

            public void OnNext(ConfigUpdate configUpdate)
            {
                if (configUpdate == null)
                {
                    _observer.OnError(new Status(StatusCode.Unavailable, "Empty ConfigUpdate resolved after LDS/RDS"));
                    return;
                }
                var defaultRoute = configUpdate.Routes?.LastOrDefault();
                if (defaultRoute?.RouteMatch == null || defaultRoute.RouteMatch.Prefix != string.Empty || defaultRoute.RouteAction?.Cluster == null)
                {
                    var routesCount = configUpdate.Routes?.Count ?? 0;
                    _observer.OnError(new Status(StatusCode.Unavailable, $"Cluster name can not be specified. Config update contains ${routesCount} routes."));
                    return;
                }
                var clusterName = defaultRoute.RouteAction.Cluster;
                var serviceConfig = GrpcServiceConfig.Create("cds_experimental", _resolverPlugin._defaultLoadBalancingPolicy);
                var config = GrpcServiceConfigOrError.FromConfig(serviceConfig);
                _resolverPlugin._logger.LogDebug($"Service config created with policies: {string.Join(',', serviceConfig.RequestedLoadBalancingPolicies)}");
                var attributes = new GrpcAttributes(new Dictionary<string, object>()
                {
                    { XdsAttributesConstants.XdsClientPoolInstance, _resolverPlugin._xdsClientPool ?? throw new ArgumentNullException(nameof(_xdsClientPool)) },
                    { XdsAttributesConstants.CdsClusterName, clusterName }
                });
                var result = new GrpcNameResolutionResult(new List<GrpcHostAddress>(), config, attributes);
                _observer.OnNext(result);
            }
        }
    }
}
