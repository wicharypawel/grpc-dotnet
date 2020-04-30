using Envoy.Api.V2;
using Envoy.Service.Discovery.V2;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        private readonly GrpcChannel _adsChannel;
        private readonly AggregatedDiscoveryService.AggregatedDiscoveryServiceClient _adsClient;
        private readonly AsyncDuplexStreamingCall<DiscoveryRequest, DiscoveryResponse> _adsStream;
        private readonly XdsBootstrapInfo _bootstrapInfo;
        private readonly ILogger _logger;

        public XdsClient(IXdsBootstrapper bootstrapper, ILoggerFactory loggerFactory)
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
            _adsChannel = GrpcChannel.ForAddress(_bootstrapInfo.Servers[0].ServerUri, channelOptions);
            _logger.LogDebug("XdsClient start ADS stream");
            _adsClient = new AggregatedDiscoveryService.AggregatedDiscoveryServiceClient(_adsChannel);
            _adsStream = _adsClient.StreamAggregatedResources();
            _logger.LogDebug("XdsClient ADS stream started");
        }

        internal bool Disposed { get; private set; }

        public async Task<List<Listener>> GetLdsAsync(string resourceName)
        {
            _logger.LogDebug("XdsClient request LDS");
            await _adsStream.RequestStream.WriteAsync(new DiscoveryRequest()
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
                .Select(x => Listener.Parser.ParseFrom(x.Value))
                .ToList();
            return listeners;
        }

        public async Task<List<RouteConfiguration>> GetRdsAsync(string listenerName)
        {
            _logger.LogDebug("XdsClient request RDS");
            await _adsStream.RequestStream.WriteAsync(new DiscoveryRequest()
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
                .Select(x => RouteConfiguration.Parser.ParseFrom(x.Value))
                .ToList();
            return routeConfigurations;
        }

        public async Task<List<Cluster>> GetCdsAsync()
        {
            _logger.LogDebug("XdsClient request CDS");
            await _adsStream.RequestStream.WriteAsync(new DiscoveryRequest()
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
                .Select(x => Cluster.Parser.ParseFrom(x.Value))
                .ToList();
            return clusters;
        }

        public async Task<List<ClusterLoadAssignment>> GetEdsAsync(string clusterName)
        {
            _logger.LogDebug("XdsClient request EDS");
            await _adsStream.RequestStream.WriteAsync(new DiscoveryRequest()
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
                .Select(x => ClusterLoadAssignment.Parser.ParseFrom(x.Value))
                .ToList();
            return clusterLoadAssignments;
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

        internal static List<Envoy.Api.V2.Route.Route> FindRoutesInRouteConfig(RouteConfiguration config, string hostName)
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
    }
}
