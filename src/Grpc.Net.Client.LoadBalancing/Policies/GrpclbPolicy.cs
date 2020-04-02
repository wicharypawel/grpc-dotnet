using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Grpc.Lb.V1;
using System.Threading;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using Grpc.Net.Client.LoadBalancing.Policies.Abstraction;
using Google.Protobuf.WellKnownTypes;

namespace Grpc.Net.Client.LoadBalancing.Policies
{
    /// <summary>
    /// The load balancing policy creates a subchannel to each server address.
    /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
    /// 
    /// Official name of this policy is "grpclb". It is a implementation of an external load balancing also called lookaside or one-arm loadbalancing.
    /// More: https://github.com/grpc/grpc/blob/master/doc/load-balancing.md#external-load-balancing-service
    /// </summary>
    public sealed class GrpclbPolicy : IGrpcLoadBalancingPolicy
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
        private IReadOnlyList<GrpcNameResolutionResult> _fallbackAddresses = Array.Empty<GrpcNameResolutionResult>();
        private bool _isFallback = false;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
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
        internal int SubChannelsCacheHash { get; private set; } = 0;
        internal IReadOnlyList<GrpcSubChannel> SubChannels { get; set; } = Array.Empty<GrpcSubChannel>();

        /// <summary>
        /// Property created for testing purposes, allows setter injection
        /// </summary>
        internal ITimer? OverrideTimer { private get; set; }

        /// <summary>
        /// Property created for testing purposes, allows setter injection
        /// </summary>
        internal ILoadBalancerClient? OverrideLoadBalancerClient { private get; set; }

        /// <summary>
        /// Creates a subchannel to each server address. Depending on policy this may require additional 
        /// steps eg. reaching out to lookaside loadbalancer.
        /// </summary>
        /// <param name="resolutionResult">Resolved list of servers and/or lookaside load balancers.</param>
        /// <param name="serviceName">The name of the load balanced service (e.g., service.googleapis.com).</param>
        /// <param name="isSecureConnection">Flag if connection between client and destination server should be secured.</param>
        /// <returns>List of subchannels.</returns>
        public async Task CreateSubChannelsAsync(List<GrpcNameResolutionResult> resolutionResult, string serviceName, bool isSecureConnection)
        {
            if (resolutionResult == null)
            {
                throw new ArgumentNullException(nameof(resolutionResult));
            }
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException($"{nameof(serviceName)} not defined");
            }
            _fallbackAddresses = resolutionResult.Where(x => !x.IsLoadBalancer).ToList();
            resolutionResult = resolutionResult.Where(x => x.IsLoadBalancer).ToList();
            if (resolutionResult.Count == 0)
            {
                throw new ArgumentException($"{nameof(resolutionResult)} must contain at least one blancer address");
            }
            _isSecureConnection = isSecureConnection;
            _logger.LogDebug($"Start grpclb policy");
            _logger.LogDebug($"Start connection to external load balancer");
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var channelOptionsForLB = new GrpcChannelOptions() { LoggerFactory = _loggerFactory };
            _loadBalancerClient = GetLoadBalancerClient($"http://{resolutionResult[0].Host}:{resolutionResult[0].Port}", channelOptionsForLB);
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

        /// <summary>
        /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
        /// </summary>
        /// <returns>Selected subchannel.</returns>
        public GrpcSubChannel GetNextSubChannel()
        {
            if (_isFallback && FallbackSubChannels.Count > 0)
            {
                return FallbackSubChannels[Interlocked.Increment(ref _subChannelsSelectionCounter) % FallbackSubChannels.Count];
            }
            Interlocked.Increment(ref _requestsCounter);
            return SubChannels[Interlocked.Increment(ref _subChannelsSelectionCounter) % SubChannels.Count];
        }

        /// <summary>
        /// Releases the resources used by the <see cref="GrpclbPolicy"/> class.
        /// </summary>
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
                _loadBalancerClient?.Dispose();
            }
            Disposed = true;
        }

        private async Task ProcessInitialResponseAsync(Core.IAsyncStreamReader<LoadBalanceResponse> responseStream)
        {
            await responseStream.MoveNext(CancellationToken.None).ConfigureAwait(false);
            if (responseStream.Current.LoadBalanceResponseTypeCase != LoadBalanceResponse.LoadBalanceResponseTypeOneofCase.InitialResponse)
            {
                throw new InvalidOperationException("InitialLoadBalanceRequest was not followed by InitialLoadBalanceResponse");
            }
            var initialResponse = responseStream.Current.InitialResponse; // field InitialResponse.LoadBalancerDelegate is deprecated
            _clientStatsReportInterval = initialResponse.ClientStatsReportInterval.ToTimeSpan();
        }

        private async Task ProcessNextBalancerResponseAsync(Core.IAsyncStreamReader<LoadBalanceResponse> responseStream)
        {
            await responseStream.MoveNext(CancellationToken.None).ConfigureAwait(false);
            switch (responseStream.Current.LoadBalanceResponseTypeCase)
            {
                case LoadBalanceResponse.LoadBalanceResponseTypeOneofCase.InitialResponse:
                    throw new InvalidOperationException("Unexpected InitialResponse");
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
            return Task.CompletedTask;
        }

        private ITimer GetTimer()
        {
            if(OverrideTimer != null)
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
                    (current * modifier) + item!.GetHashCode());
            }
        }
    }
}
