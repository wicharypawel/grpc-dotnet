﻿using Envoy.Api.V2;
using Envoy.Service.Discovery.V2;
using Grpc.Core;
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

        private readonly Channel _adsChannel;
        private readonly AggregatedDiscoveryService.AggregatedDiscoveryServiceClient _adsClient;
        private readonly AsyncDuplexStreamingCall<DiscoveryRequest, DiscoveryResponse> _adsStream;
        private readonly XdsBootstrapInfo _bootstrapInfo;

        public XdsClient(IXdsBootstrapper bootstrapper)
        {
            _bootstrapInfo = bootstrapper.ReadBootstrap();
            if(_bootstrapInfo.Servers.Count == 0)
            {
                throw new System.InvalidOperationException("XdsClient No management server provided by bootstrap");
            }
            _adsChannel = new Channel(_bootstrapInfo.Servers[0].ServerUri, ChannelCredentials.Insecure);
            _adsClient = new AggregatedDiscoveryService.AggregatedDiscoveryServiceClient(_adsChannel);
            _adsStream = _adsClient.StreamAggregatedResources();
        }

        internal bool Disposed { get; private set; }

        public async Task<List<Listener>> GetLdsAsync()
        {
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
