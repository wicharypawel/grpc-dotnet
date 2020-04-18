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
                throw new InvalidOperationException("XdsClient No management server provided by bootstrap");
            }
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var channelOptions = new GrpcChannelOptions() { LoggerFactory = loggerFactory, Credentials = ChannelCredentials.Insecure };
            _adsChannel = GrpcChannel.ForAddress(_bootstrapInfo.Servers[0].ServerUri, channelOptions);
            _logger.LogDebug("XdsClient start ADS connection");
            _adsClient = new AggregatedDiscoveryService.AggregatedDiscoveryServiceClient(_adsChannel);
            _adsStream = _adsClient.StreamAggregatedResources();
            _logger.LogDebug("XdsClient ADS started");
        }

        internal bool Disposed { get; private set; }

        public async Task<List<Listener>> GetLdsAsync()
        {
            _logger.LogDebug("XdsClient request LDS");
            await _adsStream.RequestStream.WriteAsync(new DiscoveryRequest()
            {
                TypeUrl = ADS_TYPE_URL_LDS,
                ResourceNames = { },
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
    }
}
