using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        private string version = string.Empty;
        private string nonce = string.Empty;

        private readonly ChannelBase _adsChannel;
        private readonly Envoy.Service.Discovery.V2.AggregatedDiscoveryService.AggregatedDiscoveryServiceClient _adsClient;
        private readonly AsyncDuplexStreamingCall<Envoy.Api.V2.DiscoveryRequest, Envoy.Api.V2.DiscoveryResponse> _adsStream;
        private readonly XdsBootstrapInfo _bootstrapInfo;
        private readonly ILogger _logger;

        public XdsClient(IXdsBootstrapper bootstrapper, ILoggerFactory loggerFactory, XdsChannelFactory channelFactory)
        {
            _logger = loggerFactory.CreateLogger<XdsClient>();
            _bootstrapInfo = bootstrapper.ReadBootstrap();
            if(_bootstrapInfo.Servers.Count == 0)
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
            _logger.LogDebug("XdsClient start control-plane connection");
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);                          
            var channelOptions = new GrpcChannelOptions() { LoggerFactory = loggerFactory, Credentials = ChannelCredentials.Insecure };
            _adsChannel = channelFactory.CreateChannel(_bootstrapInfo.Servers[0].ServerUri, channelOptions);
            _logger.LogDebug("XdsClient start ADS stream");
            _adsClient = new Envoy.Service.Discovery.V2.AggregatedDiscoveryService.AggregatedDiscoveryServiceClient(_adsChannel);
            _adsStream = _adsClient.StreamAggregatedResources();
            _logger.LogDebug("XdsClient ADS stream started");
        }

        internal bool Disposed { get; private set; }

        public async Task<ConfigUpdate> GetLdsRdsAsync(string resourceName)
        {
            _logger.LogDebug("XdsClient request LDS");
            await _adsStream.RequestStream.WriteAsync(new Envoy.Api.V2.DiscoveryRequest()
            {
                TypeUrl = ADS_TYPE_URL_LDS,
                ResourceNames = { resourceName },
                VersionInfo = version,
                ResponseNonce = nonce,
                Node = _bootstrapInfo.Node
            }).ConfigureAwait(false);
            await _adsStream.ResponseStream.MoveNext(CancellationToken.None).ConfigureAwait(false);
            var discoveryResponse = _adsStream.ResponseStream.Current;
            version = discoveryResponse.VersionInfo;
            nonce = discoveryResponse.Nonce;
            var listeners = discoveryResponse.Resources
                .Select(x => Envoy.Api.V2.Listener.Parser.ParseFrom(x.Value))
                .ToList();
            Envoy.Api.V2.Listener? listener = listeners.FirstOrDefault(x => x.Name.Equals(resourceName, StringComparison.OrdinalIgnoreCase));
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
                    routes = FindRoutesInRouteConfig(routeConfiguration, resourceName);
                }
                else if (hasHttpConnectionManager && httpConnectionManager!.Rds != null) // make RDS request
                {
                    _logger.LogDebug($"LDS found listener pointing to RDS");
                    var rdsConfig = httpConnectionManager!.Rds;
                    if (rdsConfig.ConfigSource?.Ads == null)
                    {
                        throw new InvalidOperationException("LDS that specify to call RDS is expected to have ADS source.");
                    }
                    var routeConfigurations = await GetRdsAsync(rdsConfig.RouteConfigName).ConfigureAwait(false);
                    if (routeConfigurations.Count == 0)
                    {
                        throw new InvalidOperationException("No route configurations found during RDS.");
                    }
                    var routeConfiguration = routeConfigurations.First(x => x.Name.Equals(rdsConfig.RouteConfigName, StringComparison.OrdinalIgnoreCase));
                    routes = FindRoutesInRouteConfig(routeConfiguration, resourceName);
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
            }
            return new ConfigUpdate(routes.Select(Route.FromEnvoyProtoRoute).ToList());
        }

        public async Task<ClusterUpdate> GetCdsAsync(string clusterName, string serviceName)
        {
            _logger.LogDebug("XdsClient request CDS");
            await _adsStream.RequestStream.WriteAsync(new Envoy.Api.V2.DiscoveryRequest()
            {
                TypeUrl = ADS_TYPE_URL_CDS,
                ResourceNames = { },
                VersionInfo = version,
                ResponseNonce = nonce,
                Node = _bootstrapInfo.Node
            }).ConfigureAwait(false);
            await _adsStream.ResponseStream.MoveNext(CancellationToken.None).ConfigureAwait(false);
            var discoveryResponse = _adsStream.ResponseStream.Current;
            version = discoveryResponse.VersionInfo;
            nonce = discoveryResponse.Nonce;
            var clusters = discoveryResponse.Resources
                .Select(x => Envoy.Api.V2.Cluster.Parser.ParseFrom(x.Value))
                .ToList();
            var cluster = clusters
                .Where(x => x.Type == Envoy.Api.V2.Cluster.Types.DiscoveryType.Eds)
                .Where(x => x?.EdsClusterConfig?.EdsConfig != null)
                .Where(x => x.LbPolicy == Envoy.Api.V2.Cluster.Types.LbPolicy.RoundRobin)
                .Where(x => IsSearchedCluster(x, clusterName, serviceName)).First();
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
            var clusterUpdate = new ClusterUpdate(clusterName, edsClusterName, "eds_experimental", null);
            return clusterUpdate;
        }

        public async Task<EndpointUpdate> GetEdsAsync(string clusterName)
        {
            _logger.LogDebug("XdsClient request EDS");
            await _adsStream.RequestStream.WriteAsync(new Envoy.Api.V2.DiscoveryRequest()
            {
                TypeUrl = ADS_TYPE_URL_EDS,
                ResourceNames = { clusterName },
                VersionInfo = version,
                ResponseNonce = nonce,
                Node = _bootstrapInfo.Node
            }).ConfigureAwait(false);
            await _adsStream.ResponseStream.MoveNext(CancellationToken.None).ConfigureAwait(false);
            var discoveryResponse = _adsStream.ResponseStream.Current;
            version = discoveryResponse.VersionInfo;
            nonce = discoveryResponse.Nonce;
            var clusterLoadAssignments = discoveryResponse.Resources
                .Select(x => Envoy.Api.V2.ClusterLoadAssignment.Parser.ParseFrom(x.Value))
                .ToList();
            var clusterLoadAssignment = clusterLoadAssignments
                .Where(x => x.Endpoints.Count != 0)
                .Where(x => x.Endpoints[0].LbEndpoints.Count != 0)
                .First();
            var localities = GetLocalitiesWithHighestPriority(clusterLoadAssignment.Endpoints);
            var firstLocality = Locality.FromEnvoyProtoLocality(localities.First().Locality);
            var firstLocalityLbEndpoints = LocalityLbEndpoints.FromEnvoyProtoLocalityLbEndpoints(localities.First());
            var localityLbEndpoints = new Dictionary<Locality, LocalityLbEndpoints>() { { firstLocality, firstLocalityLbEndpoints } };
            var endpointUpdate = new EndpointUpdate(clusterName, localityLbEndpoints, new List<DropOverload>());
            return endpointUpdate;
        }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }
            try
            {
                _adsStream?.RequestStream.CompleteAsync();
            }
            finally
            {
                _adsStream?.Dispose();
                _adsChannel?.ShutdownAsync().Wait();
            }
            Disposed = true;
        }

        private async Task<List<Envoy.Api.V2.RouteConfiguration>> GetRdsAsync(string listenerName)
        {
            _logger.LogDebug("XdsClient request RDS");
            await _adsStream.RequestStream.WriteAsync(new Envoy.Api.V2.DiscoveryRequest()
            {
                TypeUrl = ADS_TYPE_URL_RDS,
                ResourceNames = { listenerName },
                VersionInfo = version,
                ResponseNonce = nonce,
                Node = _bootstrapInfo.Node
            }).ConfigureAwait(false);
            await _adsStream.ResponseStream.MoveNext(CancellationToken.None).ConfigureAwait(false);
            var discoveryResponse = _adsStream.ResponseStream.Current;
            version = discoveryResponse.VersionInfo;
            nonce = discoveryResponse.Nonce;
            var routeConfigurations = discoveryResponse.Resources
                .Select(x => Envoy.Api.V2.RouteConfiguration.Parser.ParseFrom(x.Value))
                .ToList();
            return routeConfigurations;
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

        private IReadOnlyList<Envoy.Api.V2.Endpoint.LocalityLbEndpoints> GetLocalitiesWithHighestPriority(RepeatedField<Envoy.Api.V2.Endpoint.LocalityLbEndpoints> localities)
        {
            var groupedLocalities = localities.GroupBy(x => x.Priority).OrderBy(x => x.Key).ToList();
            _logger.LogDebug($"EDS found {groupedLocalities.Count} groups with distinct priority");
            _logger.LogDebug($"EDS select locality with priority {groupedLocalities[0].Key} [0-highest, N-lowest]");
            return groupedLocalities[0].ToList();
        }

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

        private sealed class AdsStream : IDisposable
        {
            private readonly XdsClient _parentXdsClient;
            private readonly Envoy.Service.Discovery.V2.AggregatedDiscoveryService.AggregatedDiscoveryServiceClient _adsClient;
            private readonly ILogger _logger;
            private readonly Envoy.Api.V2.Core.Node _node;
            private AsyncDuplexStreamingCall<Envoy.Api.V2.DiscoveryRequest, Envoy.Api.V2.DiscoveryResponse>? _adsStream;
            private CancellationTokenSource? _tokenSource;
            private string? _rdsResourceName;
            private bool closed = false;
            private string ldsVersion = string.Empty;
            private string rdsVersion = string.Empty;
            private string cdsVersion = string.Empty;
            private string edsVersion = string.Empty;
            private string ldsRespNonce = string.Empty;
            private string rdsRespNonce = string.Empty;
            private string cdsRespNonce = string.Empty;
            private string edsRespNonce = string.Empty;

            public AdsStream(XdsClient xdsClient, Envoy.Service.Discovery.V2.AggregatedDiscoveryService.AggregatedDiscoveryServiceClient adsClient, 
                ILogger logger, Envoy.Api.V2.Core.Node node)
            {
                _parentXdsClient = xdsClient ?? throw new ArgumentNullException(nameof(xdsClient));
                _adsClient = adsClient ?? throw new ArgumentNullException(nameof(adsClient));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _node = node ?? throw new ArgumentNullException(nameof(node));
            }

            public void Start()
            {
                _adsStream = _adsClient.StreamAggregatedResources();
                _tokenSource = new CancellationTokenSource();
                var streamObserver = this;
                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        while (await _adsStream.ResponseStream.MoveNext(_tokenSource.Token).ConfigureAwait(false))
                        {
                            streamObserver.OnNext(_adsStream.ResponseStream.Current);
                        }
                        streamObserver.OnCompleted();
                    }
                    catch (Exception exception)
                    {
                        streamObserver.OnError(exception);
                    }
                }, _tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            public void Close()
            {
                if (closed)
                {
                    return;
                }
                closed = true;
                _logger.LogDebug($"ADS stream closed requested.");
                try
                {
                    _adsStream?.RequestStream.CompleteAsync().Wait();
                }
                finally
                {
                    Dispose();
                }
            }

            public void OnNext(Envoy.Api.V2.DiscoveryResponse response)
            {
                if (closed)
                {
                    return;
                }
                string typeUrl = response.TypeUrl;
                // Nonce in each response is echoed back in the following ACK/NACK request. It is
                // used for management server to identify which response the client is ACKing/NACking.
                // To avoid confusion, client-initiated requests will always use the nonce in
                // most recently received responses of each resource type.
                if (typeUrl.Equals(ADS_TYPE_URL_LDS, StringComparison.Ordinal))
                {
                    ldsRespNonce = response.Nonce;
                    //_xdsClient.HandleLdsResponse(response);
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_RDS, StringComparison.Ordinal))
                {
                    rdsRespNonce = response.Nonce;
                    //_xdsClient.HandleRdsResponse(response);
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_CDS, StringComparison.Ordinal))
                {
                    cdsRespNonce = response.Nonce;
                    //_xdsClient.HandleCdsResponse(response);
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_EDS, StringComparison.Ordinal))
                {
                    edsRespNonce = response.Nonce;
                    //_xdsClient.HandleEdsResponse(response);
                }
                else
                {
                    _logger.LogDebug("Received an unknown type of DiscoveryResponse");
                }
            }

            public void OnError(Exception exception)
            {
                HandleStreamClosed(new Status(StatusCode.Unknown, exception.Message));
            }

            public void OnCompleted()
            {
                HandleStreamClosed(new Status(StatusCode.Unavailable, "Closed by server"));
            }

            private void HandleStreamClosed(Status error)
            {
                if (error.StatusCode == StatusCode.OK)
                {
                    throw new InvalidOperationException("Unexpected OK status");
                }
                if (closed)
                {
                    return;
                }
                closed = true;
                _logger.LogDebug($"ADS stream closed with status {error.StatusCode}: {error.Detail}.");
                Dispose();
            }

            public async Task SendXdsRequestAsync(string typeUrl, IReadOnlyList<string> resourceNames)
            {
                if (_adsStream == null)
                {
                    throw new InvalidOperationException("ADS stream has not been started");
                }
                string versionInfo = string.Empty;
                string nonce = string.Empty;
                if (typeUrl.Equals(ADS_TYPE_URL_LDS, StringComparison.Ordinal))
                {
                    versionInfo = ldsVersion;
                    nonce = ldsRespNonce;
                    _logger.LogDebug($"Sending LDS request for resources: {string.Join(',', resourceNames)}");
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_RDS, StringComparison.Ordinal))
                {
                    if (resourceNames.Count != 1)
                    {
                        throw new InvalidOperationException("RDS request requesting for more than one resource");
                    }
                    versionInfo = rdsVersion;
                    nonce = rdsRespNonce;
                    _rdsResourceName = resourceNames[0];
                    _logger.LogDebug($"Sending RDS request for resources: {string.Join(',', resourceNames)}");
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_CDS, StringComparison.Ordinal))
                {
                    versionInfo = cdsVersion;
                    nonce = cdsRespNonce;
                    _logger.LogDebug($"Sending CDS request for resources: {string.Join(',', resourceNames)}");
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_EDS, StringComparison.Ordinal))
                {
                    versionInfo = edsVersion;
                    nonce = edsRespNonce;
                    _logger.LogDebug($"Sending EDS request for resources: {string.Join(',', resourceNames)}");
                }
                var request = new Envoy.Api.V2.DiscoveryRequest()
                {
                    VersionInfo = versionInfo,
                    Node = _node,
                    TypeUrl = typeUrl,
                    ResponseNonce = nonce
                };
                request.ResourceNames.AddRange(resourceNames);
                await _adsStream.RequestStream.WriteAsync(request).ConfigureAwait(false);
                _logger.LogDebug($"Sent DiscoveryRequest");
            }

            public async Task SendAckRequestAsync(string typeUrl, IReadOnlyList<string> resourceNames, string versionInfo)
            {
                if (_adsStream == null)
                {
                    throw new InvalidOperationException("ADS stream has not been started");
                }
                string nonce = string.Empty;
                if (typeUrl.Equals(ADS_TYPE_URL_LDS, StringComparison.Ordinal))
                {
                    ldsVersion = versionInfo;
                    nonce = ldsRespNonce;
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_RDS, StringComparison.Ordinal))
                {
                    rdsVersion = versionInfo;
                    nonce = rdsRespNonce;
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_CDS, StringComparison.Ordinal))
                {
                    cdsVersion = versionInfo;
                    nonce = cdsRespNonce;
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_EDS, StringComparison.Ordinal))
                {
                    edsVersion = versionInfo;
                    nonce = edsRespNonce;
                }
                var request = new Envoy.Api.V2.DiscoveryRequest()
                {
                    VersionInfo = versionInfo,
                    Node = _node,
                    TypeUrl = typeUrl,
                    ResponseNonce = nonce
                };
                request.ResourceNames.AddRange(resourceNames);
                await _adsStream.RequestStream.WriteAsync(request).ConfigureAwait(false);
                _logger.LogDebug($"Sent ACK request");
            }

            public async Task SendNackRequestAsync(string typeUrl, IReadOnlyList<string> resourceNames, string rejectVersion, string message)
            {
                if (_adsStream == null)
                {
                    throw new InvalidOperationException("ADS stream has not been started");
                }
                string versionInfo = string.Empty;
                string nonce = string.Empty;
                if (typeUrl.Equals(ADS_TYPE_URL_LDS, StringComparison.Ordinal))
                {
                    versionInfo = ldsVersion;
                    nonce = ldsRespNonce;
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_RDS, StringComparison.Ordinal))
                {
                    versionInfo = rdsVersion;
                    nonce = rdsRespNonce;
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_CDS, StringComparison.Ordinal))
                {
                    versionInfo = cdsVersion;
                    nonce = cdsRespNonce;
                }
                else if (typeUrl.Equals(ADS_TYPE_URL_EDS, StringComparison.Ordinal))
                {
                    versionInfo = edsVersion;
                    nonce = edsRespNonce;
                }
                var request = new Envoy.Api.V2.DiscoveryRequest()
                {
                    VersionInfo = versionInfo,
                    Node = _node,
                    TypeUrl = typeUrl,
                    ResponseNonce = nonce,
                    ErrorDetail = new Google.Rpc.Status()
                    {
                        Code = (int)StatusCode.InvalidArgument,
                        Message = message
                    }
                };
                request.ResourceNames.AddRange(resourceNames);
                await _adsStream.RequestStream.WriteAsync(request).ConfigureAwait(false);
                _logger.LogDebug($"Sent NACK request");
            }

            public void Dispose()
            {
                try
                {
                    _tokenSource?.Cancel();
                }
                finally
                {
                    _tokenSource?.Dispose();
                    _adsStream?.Dispose();
                }
                // _parentXdsClient should not be disposed here
            }
        }
    }
}
