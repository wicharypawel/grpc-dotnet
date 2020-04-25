using Envoy.Api.V2;
using Envoy.Api.V2.Route;
using Envoy.Config.Filter.Network.HttpConnectionManager.V2;
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
        private XdsClientObjectPool? _xdsClientPool;
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
        /// Property created for testing purposes, allows setter injection
        /// </summary>
        internal XdsClientFactory? OverrideXdsClientFactory { private get; set; }

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
                _xdsClientPool = new XdsClientObjectPool(OverrideXdsClientFactory ?? new XdsClientFactory(_loggerFactory), _loggerFactory);
                _xdsClient = _xdsClientPool.GetObject();
            }
            _logger.LogDebug($"Start XdsResolverPlugin");
            string? clusterName = null;
            GrpcServiceConfig? serviceConfig = null;
            var listenerName = $"{target.Host}:{target.Port}";
            var listeners = await _xdsClient.GetLdsAsync(listenerName).ConfigureAwait(false);
            Listener? listener = listeners.FirstOrDefault(x => x.Name.Equals(listenerName, StringComparison.OrdinalIgnoreCase));
            if (listener != null) // LDS success, found matching listener
            {
                _logger.LogDebug($"XdsResolverPlugin found listener");
                HttpConnectionManager? httpConnectionManager = null;
                var hasHttpConnectionManager = listener.ApiListener?.ApiListener_?.TryUnpack(out httpConnectionManager) ?? false;
                if (hasHttpConnectionManager && httpConnectionManager!.RouteConfig != null) // route config in-line
                {
                    _logger.LogDebug($"XdsResolverPlugin found listener with in-line RouteConfig");
                    var routeConfiguration = httpConnectionManager!.RouteConfig;
                    clusterName = GetClusterNameFromRouteConfiguration(routeConfiguration, target);
                    serviceConfig = GrpcServiceConfig.Create("xds", _defaultLoadBalancingPolicy);
                }
                else if(hasHttpConnectionManager && httpConnectionManager!.Rds != null) // make RDS request
                {
                    _logger.LogDebug($"XdsResolverPlugin found listener pointing to RDS");
                    var rdsConfig = httpConnectionManager!.Rds;
                    if (rdsConfig.ConfigSource?.Ads == null)
                    {
                        throw new InvalidOperationException("LDS that specify to call RDS is expected to have ADS source");
                    }
                    var routeConfigurations = await _xdsClient.GetRdsAsync(rdsConfig.RouteConfigName).ConfigureAwait(false);
                    if (routeConfigurations.Count == 0)
                    {
                        throw new InvalidOperationException("No route configurations found during RDS");
                    }
                    RouteConfiguration routeConfiguration = routeConfigurations.First(x => x.Name.Equals(rdsConfig.RouteConfigName, StringComparison.OrdinalIgnoreCase));
                    clusterName = GetClusterNameFromRouteConfiguration(routeConfiguration, target);
                    serviceConfig = GrpcServiceConfig.Create("xds", _defaultLoadBalancingPolicy);
                }
                else
                {
                    throw new InvalidOperationException("LDS Listener has been found but it doesn't contain in-line configuration, nor point to RDS");
                }
            }
            else
            {
                // according to gRFC documentation we should throw error here
                // we assume everything is fine because currently used control-plane does not support this
                clusterName = "magic-value-find-cluster-by-service-name";
                serviceConfig = GrpcServiceConfig.Create("xds", _defaultLoadBalancingPolicy);
            }
            var config = GrpcServiceConfigOrError.FromConfig(serviceConfig ?? throw new InvalidOperationException("serviceConfig is null"));
            _logger.LogDebug($"Service config created with policies: {string.Join(',', serviceConfig.RequestedLoadBalancingPolicies)}");
            if (_xdsClientPool == null)
            {
                throw new InvalidOperationException("XdsClientPool not initialized");
            }
            var attributes = new GrpcAttributes(new Dictionary<string, object>() 
            { 
                { XdsAttributesConstants.XdsClientPoolInstance, _xdsClientPool },
                { XdsAttributesConstants.CdsClusterName, clusterName }
            });
            return new GrpcNameResolutionResult(new List<GrpcHostAddress>(), config, attributes);
        }

        public void Dispose()
        {
            if (_xdsClient != null)
            {
                _xdsClientPool?.ReturnObject(_xdsClient); // _xdsClientPool is responsible for calling Dispose on _xdsClient
                _xdsClient = null; // object returned to the pool should not be used
            }
        }

        private string GetClusterNameFromRouteConfiguration(RouteConfiguration routeConfiguration, Uri target)
        {
            if (routeConfiguration == null)
            {
                throw new ArgumentNullException(nameof(routeConfiguration));
            }
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            var virtualHost = routeConfiguration.VirtualHosts.First(x => AnyDomainMatch(x.Domains, target.Host));
            var defaultRoute = virtualHost.Routes.Last();
            if (defaultRoute?.Match == null || defaultRoute.Match.Prefix != string.Empty || defaultRoute.Route_?.Cluster == null)
            {
                throw new InvalidOperationException("Cluster name can not be specified");
            }
            var clusterName = defaultRoute.Route_.Cluster;
            return clusterName;
        }

        private static bool AnyDomainMatch(IEnumerable<string> domains, string targetDomain)
        {
            foreach (var domain in domains)
            {
                if (domain.Contains(targetDomain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        internal static List<Route> FindRoutesInRouteConfig(RouteConfiguration config, string hostName)
        {
            var matchingLenght = -1; // longest length of wildcard pattern that matches host name
            var exactMatchFound = false;  // true if a virtual host with exactly matched domain found
            VirtualHost? targetVirtualHost = null;  // target VirtualHost with longest matched domain
            foreach (var virtualHost in config.VirtualHosts)
            {
                foreach (var domain in virtualHost.Domains)
                {
                    var isSelected = false;
                    if (MatchHostName(hostName, domain))
                    {
                        if (!domain.Contains('*', StringComparison.Ordinal)) // exact matching
                        { 
                            exactMatchFound = true;
                            targetVirtualHost = virtualHost;
                            break;
                        }
                        else if (domain.Length > matchingLenght) // longer matching pattern
                        { 
                            isSelected = true;
                        }
                        else if (domain.Length == matchingLenght && domain.StartsWith('*')) // suffix matching
                        { 
                            isSelected = true;
                        }
                    }
                    if (isSelected)
                    {
                        matchingLenght = domain.Length;
                        targetVirtualHost = virtualHost;
                    }
                }
                if (exactMatchFound)
                {
                    break;
                }
            }
            // Proceed with the virtual host that has longest wildcard matched domain name with the
            // hostname in original "xds:" URI.
            // Note we would consider upstream cluster not found if the virtual host is not configured
            // correctly for gRPC, even if there exist other virtual hosts with (lower priority)
            // matching domains.
            var routes = new List<Route>();
            if (targetVirtualHost != null)
            {
                foreach (var route in targetVirtualHost.Routes)
                {
                    routes.Add(route);
                }
            }
            return routes;
        }

        /// <summary>
        /// Matches hostName with pattern.
        /// 
        /// Wildcard pattern rules:
        ///  - A single asterisk (*) matches any domain.
        ///  - Asterisk (*) is only permitted in the left-most or the right-most part of the pattern, but not both.
        /// </summary>
        /// <param name="hostName">Not empty hostName string. Can not start or end with dot.</param>
        /// <param name="pattern">Not empty pattern string. Can not start or end with dot.</param>
        /// <returns>Returns true if hostName matches the domain name pattern with case-insensitive.</returns>
        internal static bool MatchHostName(string hostName, string pattern)
        {
            if (hostName == null || hostName.Length == 0 || hostName.StartsWith('.') || hostName.EndsWith('.'))
            {
                throw new ArgumentException("Invalid host name");
            }
            if (pattern == null || pattern.Length == 0 || pattern.StartsWith('.') || pattern.EndsWith('.'))
            {
                throw new ArgumentException("Invalid pattern/domain name");
            }
            // hostName and pattern are now in lower case -- domain names are case-insensitive.
            hostName = hostName.ToLowerInvariant();
            pattern = pattern.ToLowerInvariant();
            if (!pattern.Contains('*', StringComparison.Ordinal))
            {
                // Not a wildcard pattern -- hostName and pattern must match exactly.
                return hostName.Equals(pattern, StringComparison.Ordinal);
            }
            // Wildcard pattern
            if (pattern.Length == 1)
            {
                return true;
            }
            int wildcardIndex = pattern.IndexOf('*', StringComparison.Ordinal);
            // At most one asterisk (*) is allowed.
            if (pattern.IndexOf('*', wildcardIndex + 1) != -1)
            {
                return false;
            }
            // Asterisk can only match prefix or suffix.
            if (wildcardIndex != 0 && wildcardIndex != pattern.Length - 1)
            {
                return false;
            }
            // HostName must be at least as long as the pattern because asterisk has to
            // match one or more characters.
            if (hostName.Length < pattern.Length)
            {
                return false;
            }
            if (wildcardIndex == 0 && hostName.EndsWith(pattern.Substring(1), StringComparison.Ordinal))
            {
                return true;
            }
            return wildcardIndex == pattern.Length - 1
                && hostName.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.Ordinal);
        }
    }
}
