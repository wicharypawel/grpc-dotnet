using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        private IXdsClient? _xdsClient;
        private readonly string _defaultLoadBalancingPolicy;

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
        /// Property created for testing purposes, allows setter injection
        /// </summary>
        internal IXdsClient? OverrideXdsClient { private get; set; }

        /// <summary>
        /// Creates a <seealso cref="XdsResolverPlugin"/> using default <seealso cref="XdsResolverPluginOptions"/>.
        /// </summary>
        public XdsResolverPlugin() : this(new XdsResolverPluginOptions())
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
        /// Creates a <seealso cref="XdsResolverPlugin"/> using specified <seealso cref="XdsResolverPluginOptions"/>.
        /// </summary>
        /// <param name="options">Options allows override default behaviour.</param>
        public XdsResolverPlugin(XdsResolverPluginOptions options)
        {
            _options = options;
            _defaultLoadBalancingPolicy = "pick_first";
        }

        /// <summary>
        /// Name resolution for secified target.
        /// 
        /// Note that the xds resolver will return an empty list of addresses, because in the xDS API flow, 
        /// the addresses are not returned until the ClusterLoadAssignment resource is obtained later.
        /// </summary>
        /// <param name="target">Server address with scheme.</param>
        /// <returns>List of resolved servers.</returns>
        public async Task<GrpcNameResolutionResult> StartNameResolutionAsync(Uri target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (!target.Scheme.Equals("xds", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{nameof(XdsResolverPlugin)} require xds:// scheme to set as target address");
            }
            if (_xdsClient == null)
            {
                _xdsClient = OverrideXdsClient ?? XdsClientFactory.CreateXdsClient(_loggerFactory);
            }
            var listeners = await _xdsClient.GetLdsAsync().ConfigureAwait(false);
            var containsListenerForTarget = listeners.Any(x => x.Name.Contains(target.Host, StringComparison.OrdinalIgnoreCase));
            var host = target.Host;
            var serviceConfig = GrpcServiceConfig.Create("xds", _defaultLoadBalancingPolicy);
            _logger.LogDebug($"NameResolution xds returns empty resolution result list");
            _logger.LogDebug($"Service config created with policies: {string.Join(',', serviceConfig.RequestedLoadBalancingPolicies)}");
            var config = containsListenerForTarget ? GrpcServiceConfigOrError.FromConfig(serviceConfig) : GrpcServiceConfigOrError.FromError(Core.Status.DefaultCancelled);
            var attributes = new GrpcAttributes(new Dictionary<string, object>() { { XdsAttributesConstants.XdsClientInstanceKey, _xdsClient } });
            return new GrpcNameResolutionResult(new List<GrpcHostAddress>(), config, attributes);
        }

        public void Dispose()
        {
            _xdsClient?.Dispose();
        }
    }
}
