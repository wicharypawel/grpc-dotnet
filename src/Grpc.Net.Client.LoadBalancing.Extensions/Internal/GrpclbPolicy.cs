﻿using Google.Protobuf.WellKnownTypes;
using Grpc.Lb.V1;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal.Abstraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    /// <summary>
    /// The load balancing policy creates a subchannel to each server address.
    /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
    /// 
    /// Official name of this policy is "grpclb". It is a implementation of an external load balancing also called lookaside or one-arm loadbalancing.
    /// More: https://github.com/grpc/grpc/blob/master/doc/load-balancing.md#external-load-balancing-service
    /// </summary>
    internal sealed class GrpclbPolicy : IGrpcLoadBalancingPolicy
    {
        private TimeSpan _clientStatsReportInterval = TimeSpan.Zero;
        private bool _isSecureConnection = false;
        private int _requestsCounter = 0;
        private int _subChannelsSelectionCounter = -1;
        private ILogger _logger = NullLogger.Instance;
        private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
        private ILoadBalancerClient? _loadBalancerClient;
        private IAsyncDuplexStreamingCall<LoadBalanceRequest, LoadBalanceResponse>? _balancingStreaming;
        private ITimer? _timer;
        private IReadOnlyList<GrpcHostAddress> _fallbackAddresses = Array.Empty<GrpcHostAddress>();
        private bool _isFallback = false;

        public ILoggerFactory LoggerFactory
        {
            set
            {
                _loggerFactory = value;
                _logger = value.CreateLogger<GrpclbPolicy>();
            }
        }
        internal bool Disposed { get; private set; }
        internal IReadOnlyList<GrpcSubChannel> FallbackSubChannels { get; set; } = Array.Empty<GrpcSubChannel>();
        internal IReadOnlyList<GrpcPickResult> FallbackPickResults { get; set; } = Array.Empty<GrpcPickResult>();
        internal int SubChannelsCacheHash { get; private set; } = 0;
        internal IReadOnlyList<GrpcSubChannel> SubChannels { get; set; } = Array.Empty<GrpcSubChannel>();
        internal IReadOnlyList<GrpcPickResult> PickResults { get; set; } = Array.Empty<GrpcPickResult>();

        internal ITimer? OverrideTimer { private get; set; }

        internal ILoadBalancerClient? OverrideLoadBalancerClient { private get; set; }

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
            _fallbackAddresses = hostsAddresses.Where(x => !x.IsLoadBalancer).ToList();
            hostsAddresses = hostsAddresses.Where(x => x.IsLoadBalancer).ToList();
            if (hostsAddresses.Count == 0)
            {
                throw new ArgumentException($"{nameof(resolutionResult)} must contain at least one blancer address.");
            }
            _isSecureConnection = isSecureConnection;
            _logger.LogDebug($"Start grpclb policy");
            _logger.LogDebug($"Start connection to external load balancer");
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var channelOptionsForLB = new GrpcChannelOptions() { LoggerFactory = _loggerFactory };
            _loadBalancerClient = GetLoadBalancerClient($"http://{hostsAddresses[0].Host}:{hostsAddresses[0].Port}", channelOptionsForLB);
            _balancingStreaming = _loadBalancerClient.BalanceLoad();
            var initialRequest = new InitialLoadBalanceRequest() { Name = serviceName };
            await _balancingStreaming.RequestStream.WriteAsync(new LoadBalanceRequest() { InitialRequest = initialRequest }).ConfigureAwait(false);
            await ProcessInitialResponseAsync(_balancingStreaming.ResponseStream).ConfigureAwait(false);
            await ProcessNextBalancerResponseAsync(_balancingStreaming.ResponseStream).ConfigureAwait(false);
            _logger.LogDebug($"SubChannels list created");
            if (_clientStatsReportInterval > TimeSpan.Zero)
            {
                _timer = GetTimer();
                _timer.Start(ReportClientStatsTimerAsync, null, _clientStatsReportInterval, _clientStatsReportInterval);
                _logger.LogDebug($"Periodic ClientStats reporting enabled, interval was set to {_clientStatsReportInterval}");
            }
        }

        public GrpcPickResult GetNextSubChannel()
        {
            if (_isFallback && FallbackPickResults.Count > 0)
            {
                return FallbackPickResults[Interlocked.Increment(ref _subChannelsSelectionCounter) % FallbackPickResults.Count];
            }
            Interlocked.Increment(ref _requestsCounter);
            return PickResults[Interlocked.Increment(ref _subChannelsSelectionCounter) % PickResults.Count];
        }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }
            try
            {
                _timer?.Change(Timeout.Infinite, 0);
                _balancingStreaming?.RequestStream.CompleteAsync().Wait(); // close request stream to complete gracefully
            }
            finally
            {
                _timer?.Dispose();
                _balancingStreaming?.Dispose();
                _loadBalancerClient?.Dispose();
            }
            Disposed = true;
        }

        private async Task ProcessInitialResponseAsync(Core.IAsyncStreamReader<LoadBalanceResponse> responseStream)
        {
            await responseStream.MoveNext(CancellationToken.None).ConfigureAwait(false);
            if (responseStream.Current.LoadBalanceResponseTypeCase != LoadBalanceResponse.LoadBalanceResponseTypeOneofCase.InitialResponse)
            {
                throw new InvalidOperationException("InitialLoadBalanceRequest was not followed by InitialLoadBalanceResponse.");
            }
            var initialResponse = responseStream.Current.InitialResponse;
            _clientStatsReportInterval = initialResponse.ClientStatsReportInterval.ToTimeSpan();
        }

        private async Task ProcessNextBalancerResponseAsync(Core.IAsyncStreamReader<LoadBalanceResponse> responseStream)
        {
            await responseStream.MoveNext(CancellationToken.None).ConfigureAwait(false);
            switch (responseStream.Current.LoadBalanceResponseTypeCase)
            {
                case LoadBalanceResponse.LoadBalanceResponseTypeOneofCase.InitialResponse:
                    throw new InvalidOperationException("Unexpected InitialResponse.");
                case LoadBalanceResponse.LoadBalanceResponseTypeOneofCase.ServerList:
                    _isFallback = false;
                    await UseServerListSubChannelsAsync(responseStream.Current.ServerList).ConfigureAwait(false);
                    break;
                case LoadBalanceResponse.LoadBalanceResponseTypeOneofCase.FallbackResponse:
                    _isFallback = true;
                    await UseFallbackSubChannelsAsync().ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }

        // async void recommended by Stephen Cleary https://stackoverflow.com/questions/38917818/pass-async-callback-to-timer-constructor
        private async void ReportClientStatsTimerAsync(object state)
        {
            await ReportClientStatsAsync().ConfigureAwait(false);
            await ProcessNextBalancerResponseAsync(_balancingStreaming!.ResponseStream).ConfigureAwait(false);
        }

        private async Task ReportClientStatsAsync()
        {
            var requestsCounter = Interlocked.Exchange(ref _requestsCounter, 0);
            var clientStats = new ClientStats()
            {
                NumCallsStarted = requestsCounter,
                NumCallsFinished = requestsCounter,
                NumCallsFinishedKnownReceived = requestsCounter,
                NumCallsFinishedWithClientFailedToSend = 0,
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
            };
            await _balancingStreaming!.RequestStream.WriteAsync(new LoadBalanceRequest() { ClientStats = clientStats }).ConfigureAwait(false);
        }

        private Task UseServerListSubChannelsAsync(ServerList serverList)
        {
            _logger.LogDebug($"Grpclb received ServerList");
            var serverListHash = GetSequenceHashCode(serverList.Servers);
            if (serverListHash == SubChannelsCacheHash)
            {
                _logger.LogDebug($"ServerList hasn't been changed, subchannels remain the same");
                return Task.CompletedTask;
            }
            SubChannelsCacheHash = serverListHash;
            var result = new List<GrpcSubChannel>();
            foreach (var server in serverList.Servers)
            {
                var ipAddress = new IPAddress(server.IpAddress.ToByteArray()).ToString();
                var uriBuilder = new UriBuilder();
                uriBuilder.Host = ipAddress;
                uriBuilder.Port = server.Port;
                uriBuilder.Scheme = _isSecureConnection ? "https" : "http";
                var uri = uriBuilder.Uri;
                result.Add(new GrpcSubChannel(uri, server.LoadBalanceToken));
                _logger.LogDebug($"Found a server {uri}");
            }
            SubChannels = result;
            PickResults = result.Select(x => GrpcPickResult.WithSubChannel(x)).ToArray();
            return Task.CompletedTask;
        }

        private Task UseFallbackSubChannelsAsync()
        {
            _logger.LogDebug($"Grpclb fallback requested");
            FallbackSubChannels = _fallbackAddresses.Select(x =>
            {
                var uriBuilder = new UriBuilder();
                uriBuilder.Host = x.Host;
                uriBuilder.Port = x.Port ?? (_isSecureConnection ? 443 : 80);
                uriBuilder.Scheme = _isSecureConnection ? "https" : "http";
                var uri = uriBuilder.Uri;
                _logger.LogDebug($"Using fallback server {uri}");
                return new GrpcSubChannel(uri);
            }).ToList();
            FallbackPickResults = FallbackSubChannels.Select(x => GrpcPickResult.WithSubChannel(x)).ToArray();
            return Task.CompletedTask;
        }

        private ITimer GetTimer()
        {
            if (OverrideTimer != null)
            {
                return OverrideTimer;
            }
            return new WrappedTimer();
        }

        private ILoadBalancerClient GetLoadBalancerClient(string address, GrpcChannelOptions channelOptionsForLB)
        {
            if (OverrideLoadBalancerClient != null)
            {
                return OverrideLoadBalancerClient;
            }
            return new WrappedLoadBalancerClient(address, channelOptionsForLB);
        }

        private static int GetSequenceHashCode<T>(IList<T> sequence)
        {
            const int seed = 487;
            const int modifier = 31;

            unchecked
            {
                return sequence.Aggregate(seed, (current, item) =>
                    current * modifier + item!.GetHashCode());
            }
        }
    }
}
