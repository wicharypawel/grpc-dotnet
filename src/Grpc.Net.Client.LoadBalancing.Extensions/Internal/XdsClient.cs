using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Grpc.Net.Client.LoadBalancing.Extensions.Internal.EnvoyProtoData;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class XdsClient : IXdsClient
    {
        private static readonly string ADS_TYPE_URL_LDS = "type.googleapis.com/envoy.api.v2.Listener";
        private static readonly string ADS_TYPE_URL_RDS = "type.googleapis.com/envoy.api.v2.RouteConfiguration";
        private static readonly string ADS_TYPE_URL_CDS = "type.googleapis.com/envoy.api.v2.Cluster";
        private static readonly string ADS_TYPE_URL_EDS = "type.googleapis.com/envoy.api.v2.ClusterLoadAssignment";

        private readonly ChannelBase _adsChannel;
        private AdsStreamWrapper? _adsStreamWrapper;
        private readonly XdsBootstrapInfo _bootstrapInfo;
        private readonly ILogger _logger;

        //temp crap
        private ConfigUpdate? _configUpdate = null;
        private ClusterUpdate? _clusterUpdate = null;
        private EndpointUpdate? _endpointUpdate = null;
        private string _clusterName = string.Empty;
        private string _serviceName = string.Empty;
        private string _resourceName = string.Empty;
        private string _routeConfigName = string.Empty;

        public XdsClient(IXdsBootstrapper bootstrapper, ILoggerFactory loggerFactory, XdsChannelFactory channelFactory)
        {
            _logger = loggerFactory.CreateLogger<XdsClient>();
            _bootstrapInfo = bootstrapper.ReadBootstrap();
            if (_bootstrapInfo.Servers.Count == 0)
            {
                throw new InvalidOperationException("XdsClient No management server provided by bootstrap.");
            }
            if (_bootstrapInfo.Servers[0].ChannelCredsList.Count != 0)
            {
                // materials google_default:
                // start by creating service_account json file in GCP
                // visit links below, start with methods  
                // CreateDefaultCredentialAsync, CreateDefaultCredentialFromFile, CreateDefaultCredentialFromParameters, CreateServiceAccountCredentialFromParameters
                // https://github.com/googleapis/google-api-dotnet-client/blob/master/Src/Support/Google.Apis.Auth/OAuth2/GoogleCredential.cs
                // https://github.com/googleapis/google-api-dotnet-client/blob/master/Src/Support/Google.Apis.Auth/OAuth2/DefaultCredentialProvider.cs
                throw new NotImplementedException("XdsClient Channel credentials are not supported.");
            }
            _logger.LogDebug("Create channel to control-plane");
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var channelOptions = new GrpcChannelOptions() { LoggerFactory = loggerFactory, Credentials = ChannelCredentials.Insecure };
            _adsChannel = channelFactory.CreateChannel(_bootstrapInfo.Servers[0].ServerUri, channelOptions);
        }

        internal bool Disposed { get; private set; }

        public async Task<ConfigUpdate> GetLdsRdsAsync(string resourceName)
        {
            _logger.LogDebug("XdsClient request LDS");
            if (_adsStreamWrapper == null)
            {
                StartRpcStream();
            }
            _resourceName = resourceName;
            await _adsStreamWrapper!.SendXdsRequestAsync(ADS_TYPE_URL_LDS, new List<string>() { resourceName }).ConfigureAwait(false);
            while (_configUpdate == null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
            }
            var result = _configUpdate;
            _configUpdate = null;
            return result;
        }
        
        internal void HandleLdsResponse(Envoy.Api.V2.DiscoveryResponse discoveryResponse)
        {
            var listeners = discoveryResponse.Resources
                .Select(x => Envoy.Api.V2.Listener.Parser.ParseFrom(x.Value))
                .ToList();
            Envoy.Api.V2.Listener? listener = listeners.FirstOrDefault(x => x.Name.Equals(_resourceName, StringComparison.OrdinalIgnoreCase));
            List<Envoy.Api.V2.Route.Route> routes = new List<Envoy.Api.V2.Route.Route>();
            if (listener != null) // LDS success, found matching listener
            {
                _logger.LogDebug($"LDS found listener");
                Envoy.Config.Filter.Network.HttpConnectionManager.V2.HttpConnectionManager? httpConnectionManager = null;
                var hasHttpConnectionManager = listener.ApiListener?.ApiListener_?.TryUnpack(out httpConnectionManager) ?? false;
                if (hasHttpConnectionManager && httpConnectionManager!.RouteConfig != null) // route config in-line
                {
                    _logger.LogDebug($"LDS found listener with in-line RouteConfig");
                    var routeConfiguration = httpConnectionManager!.RouteConfig;
                    var hostName = _resourceName.Substring(0, _resourceName.LastIndexOf(':'));
                    routes = FindRoutesInRouteConfig(routeConfiguration, hostName);
                    _configUpdate = new ConfigUpdate(routes.Select(Route.FromEnvoyProtoRoute).ToList());
                }
                else if (hasHttpConnectionManager && httpConnectionManager!.Rds != null) // make RDS request
                {
                    _logger.LogDebug($"LDS found listener pointing to RDS");
                    var rdsConfig = httpConnectionManager!.Rds;
                    if (rdsConfig.ConfigSource?.Ads == null)
                    {
                        throw new InvalidOperationException("LDS that specify to call RDS is expected to have ADS source.");
                    }
                    _routeConfigName = rdsConfig.RouteConfigName;
                    _adsStreamWrapper!.SendXdsRequestAsync(ADS_TYPE_URL_RDS, new List<string>() { _routeConfigName }).Wait();
                    return;
                }
                else
                {
                    throw new InvalidOperationException("LDS Listener has been found but it doesn't contain in-line configuration, nor point to RDS.");
                }
            }
            else
            {
                _logger.LogDebug($"LDS not found listener, trying to use legacy istio-pilot");
                routes = new List<Envoy.Api.V2.Route.Route>()
                {
                    new Envoy.Api.V2.Route.Route()
                    {
                        Match = new Envoy.Api.V2.Route.RouteMatch() { Prefix = ""},
                        Route_ = new Envoy.Api.V2.Route.RouteAction() { Cluster = "magic-value-find-cluster-by-service-name" }
                    }
                };
                _configUpdate = new ConfigUpdate(routes.Select(Route.FromEnvoyProtoRoute).ToList());
            }
        }

        internal void HandleRdsResponse(Envoy.Api.V2.DiscoveryResponse discoveryResponse)
        {
            var routeConfigurations = discoveryResponse.Resources
                .Select(x => Envoy.Api.V2.RouteConfiguration.Parser.ParseFrom(x.Value))
                .ToList();
            if (routeConfigurations.Count == 0)
            {
                throw new InvalidOperationException("No route configurations found during RDS.");
            }
            var routeConfiguration = routeConfigurations.First(x => x.Name.Equals(_routeConfigName, StringComparison.OrdinalIgnoreCase));
            var hostName = _resourceName.Substring(0, _resourceName.LastIndexOf(':'));
            var routes = FindRoutesInRouteConfig(routeConfiguration, hostName);
            _configUpdate = new ConfigUpdate(routes.Select(Route.FromEnvoyProtoRoute).ToList());
        }

        public async Task<ClusterUpdate> GetCdsAsync(string clusterName, string serviceName)
        {
            _logger.LogDebug("XdsClient request CDS");
            if (_adsStreamWrapper == null)
            {
                StartRpcStream();
            }
            _clusterName = clusterName;
            _serviceName = serviceName;
            await _adsStreamWrapper!.SendXdsRequestAsync(ADS_TYPE_URL_CDS, new List<string>() { clusterName }).ConfigureAwait(false);
            while (_clusterUpdate == null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
            }
            var result = _clusterUpdate;
            _clusterUpdate = null;
            return result;
        }

        internal void HandleCdsResponse(Envoy.Api.V2.DiscoveryResponse discoveryResponse)
        {
            var clusters = discoveryResponse.Resources
                .Select(x => Envoy.Api.V2.Cluster.Parser.ParseFrom(x.Value))
                .ToList();
            var cluster = clusters
                .Where(x => x.Type == Envoy.Api.V2.Cluster.Types.DiscoveryType.Eds)
                .Where(x => x?.EdsClusterConfig?.EdsConfig != null)
                .Where(x => x.LbPolicy == Envoy.Api.V2.Cluster.Types.LbPolicy.RoundRobin)
                .Where(x => IsSearchedCluster(x, _clusterName, _serviceName)).First();
            if (cluster.LrsServer == null)
            {
                _logger.LogDebug("LRS load reporting disabled");
            }
            else
            {
                if (cluster.LrsServer.Self == null)
                {
                    _logger.LogDebug("LRS load reporting disabled (LRS to different management server isn't supported)");
                }
                else
                {
                    _logger.LogDebug("LRS load reporting disabled (LRS to the same management server isn't supported)");
                }
            }
            string? edsClusterName = cluster.EdsClusterConfig?.ServiceName;
            var clusterUpdate = new ClusterUpdate(_clusterName, edsClusterName, "eds_experimental", null);
            _clusterUpdate = clusterUpdate;
        }

        public async Task<EndpointUpdate> GetEdsAsync(string clusterName)
        {
            _logger.LogDebug("XdsClient request EDS");
            if (_adsStreamWrapper == null)
            {
                StartRpcStream();
            }
            await _adsStreamWrapper!.SendXdsRequestAsync(ADS_TYPE_URL_EDS, new List<string>() { clusterName }).ConfigureAwait(false);
            while (_endpointUpdate == null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
            }
            var result = _endpointUpdate;
            _endpointUpdate = null;
            return result;
        }

        internal void HandleEdsResponse(Envoy.Api.V2.DiscoveryResponse discoveryResponse)
        {

            var clusterLoadAssignments = discoveryResponse.Resources
                .Select(x => Envoy.Api.V2.ClusterLoadAssignment.Parser.ParseFrom(x.Value))
                .ToList();
            var clusterLoadAssignment = clusterLoadAssignments
                .Where(x => x.Endpoints.Count != 0)
                .Where(x => x.Endpoints[0].LbEndpoints.Count != 0)
                .First();
            var localities = GetLocalitiesWithHighestPriority(clusterLoadAssignment.Endpoints, _logger);
            var firstLocality = Locality.FromEnvoyProtoLocality(localities.First().Locality);
            var firstLocalityLbEndpoints = LocalityLbEndpoints.FromEnvoyProtoLocalityLbEndpoints(localities.First());
            var localityLbEndpoints = new Dictionary<Locality, LocalityLbEndpoints>() { { firstLocality, firstLocalityLbEndpoints } };
            var endpointUpdate = new EndpointUpdate(clusterLoadAssignment.ClusterName, localityLbEndpoints, new List<DropOverload>());
            _endpointUpdate = endpointUpdate;
        }

        public void Subscribe(string targetAuthority, ConfigUpdateObserver observer)
        {
            throw new NotImplementedException();
        }

        public void Subscribe(string clusterName, ClusterUpdateObserver observer)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe(string clusterName, ClusterUpdateObserver observer)
        {
            throw new NotImplementedException();
        }

        public void Subscribe(string clusterName, EndpointUpdateObserver observer)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe(string clusterName, EndpointUpdateObserver observer)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }
            Disposed = true;
            try
            {
                _adsStreamWrapper?.Dispose();
                _adsChannel?.ShutdownAsync().Wait();
            }
            finally
            {
                (_adsChannel as GrpcChannel)?.Dispose();
            }
        }

        private void StartRpcStream()
        {
            if (_adsStreamWrapper != null)
            {
                throw new InvalidOperationException("Previous adsStream has not been cleared yet");
            };
            var adsClient = new Envoy.Service.Discovery.V2.AggregatedDiscoveryService.AggregatedDiscoveryServiceClient(_adsChannel);
            _adsStreamWrapper = new AdsStreamWrapper(this, adsClient, _logger, _bootstrapInfo.Node);
            _adsStreamWrapper.Start();
            _logger.LogDebug("ADS stream started");
        }

        private static bool IsSearchedCluster(Envoy.Api.V2.Cluster x, string clusterName, string serviceName)
        {
            if (x == null)
            {
                return false;
            }
            if (clusterName == "magic-value-find-cluster-by-service-name") // if true it means LDS and RDS were not supported
            {
                return x.Name?.Contains(serviceName, StringComparison.OrdinalIgnoreCase) ?? false; // workaround
            }
            else
            {
                return x.Name?.Equals(clusterName, StringComparison.OrdinalIgnoreCase) ?? false; // according to docs
            }
        }

        private static IReadOnlyList<Envoy.Api.V2.Endpoint.LocalityLbEndpoints> GetLocalitiesWithHighestPriority(RepeatedField<Envoy.Api.V2.Endpoint.LocalityLbEndpoints> localities, ILogger logger)
        {
            var groupedLocalities = localities.GroupBy(x => x.Priority).OrderBy(x => x.Key).ToList();
            logger.LogDebug($"EDS found {groupedLocalities.Count} groups with distinct priority");
            logger.LogDebug($"EDS select locality with priority {groupedLocalities[0].Key} [0-highest, N-lowest]");
            return groupedLocalities[0].ToList();
        }

        /// <summary>
        /// Processes a RouteConfiguration message to find the routes that requests for the given host will
        /// be routed to. Method has internal access in order to be visible for testing.
        /// </summary>
        /// <param name="config">Message that contains routes.</param>
        /// <param name="hostName">Not empty hostName string. Can not start or end with dot.</param>
        /// <returns>List of routes for matching hostname.</returns>
        internal static List<Envoy.Api.V2.Route.Route> FindRoutesInRouteConfig(Envoy.Api.V2.RouteConfiguration config, string hostName)
        {
            var matchingLenght = -1; // longest length of wildcard pattern that matches host name
            var exactMatchFound = false;  // true if a virtual host with exactly matched domain found
            Envoy.Api.V2.Route.VirtualHost? targetVirtualHost = null;  // target VirtualHost with longest matched domain
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
            var routes = new List<Envoy.Api.V2.Route.Route>();
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
        /// Matches hostName with pattern. Method has internal access in order to be visible for testing.
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
                throw new ArgumentException("Invalid host name.");
            }
            if (pattern == null || pattern.Length == 0 || pattern.StartsWith('.') || pattern.EndsWith('.'))
            {
                throw new ArgumentException("Invalid pattern/domain name.");
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
