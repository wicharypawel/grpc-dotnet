#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using Grpc.Net.Client.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port).
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// </summary>
    internal sealed class DnsResolverPlugin : IGrpcResolverPlugin
    {
        private static readonly int DefaultNetworkTtlSeconds = 30;
        private readonly int _networkTtlSeconds;
        private readonly ITimer _timer;
        private ILogger _logger = NullLogger.Instance;
        private readonly string _defaultLoadBalancingPolicy;
        private Uri? _target = null;
        private IGrpcNameResolutionObserver? _observer = null;
        private CancellationTokenSource? _cancellationTokenSource = null;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<DnsResolverPlugin>();
        }

        /// <summary>
        /// Property created for testing purposes, allows setter injection
        /// </summary>
        internal Task<IPAddress[]>? OverrideDnsResults { private get; set; }

        /// <summary>
        /// Creates a new <seealso cref="DnsResolverPlugin"/> instance, with default settings.
        /// </summary>
        public DnsResolverPlugin() : this(GrpcAttributes.Empty, new SystemTimer())
        {
        }

        /// <summary>
        /// Creates a <seealso cref="DnsResolverPlugin"/> using specified <seealso cref="GrpcAttributes"/>.
        /// </summary>
        /// <param name="attributes">Attributes with options.</param>
        /// <param name="timer">Timer object required for periodic re-resolve.</param>
        public DnsResolverPlugin(GrpcAttributes attributes, ITimer timer)
        {
            _defaultLoadBalancingPolicy = attributes.Get(GrpcAttributesConstants.DefaultLoadBalancingPolicy) ?? "pick_first";
            _networkTtlSeconds = int.TryParse(attributes.Get(GrpcAttributesConstants.DnsResolverNetworkTtlSeconds), out int ttlValue) ? ttlValue : DefaultNetworkTtlSeconds;
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        }

        public void Subscribe(Uri target, IGrpcNameResolutionObserver observer)
        {
            if (_observer != null)
            {
                throw new InvalidOperationException("Observer already registered.");
            }
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _cancellationTokenSource = new CancellationTokenSource();
            _timer.Start((state) => { Resolve(); }, null, TimeSpan.Zero, TimeSpan.FromSeconds(_networkTtlSeconds));
        }

        public void Unsubscribe()
        {
            _observer = null;
            _target = null;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _timer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
        }

        public void RefreshResolution()
        {
            if (_observer == null)
            {
                throw new InvalidOperationException("Observer not registered.");
            }
            Resolve();
        }

        private void Resolve()
        {
            Task.Factory.StartNew(async () => await ResolveCoreAsync(_target, _observer).ConfigureAwait(false), _cancellationTokenSource!.Token);
        }

        private async Task ResolveCoreAsync(Uri? target, IGrpcNameResolutionObserver? observer)
        {
            if (observer == null)
            {
                return;
            }
            if (target == null)
            {
                observer.OnError(new Core.Status(Core.StatusCode.Unavailable, "Target is empty."));
                return;
            }
            if (!target.Scheme.Equals("dns", StringComparison.OrdinalIgnoreCase))
            {
                observer.OnError(new Core.Status(Core.StatusCode.Unavailable, $"{nameof(DnsResolverPlugin)} require dns:// scheme to set as target address."));
                return;
            }
            var serversDnsQuery = target.Host;
            _logger.LogDebug($"Start A lookup for {serversDnsQuery}");
            try
            {
                var serversDnsQueryTask = OverrideDnsResults ?? Dns.GetHostAddressesAsync(serversDnsQuery);
                await serversDnsQueryTask.ConfigureAwait(false);
                var serversDnsQueryResults = serversDnsQueryTask.Result.Select(x => ParseARecord(x, target.Port)).ToArray();
                var results = serversDnsQueryResults.ToList();
                _logger.LogDebug($"NameResolution found {results.Count} DNS records");
                var serviceConfig = GrpcServiceConfig.Create(_defaultLoadBalancingPolicy);
                _logger.LogDebug($"Service config created with policies: {string.Join(',', serviceConfig.RequestedLoadBalancingPolicies)}");
                observer.OnNext(new GrpcNameResolutionResult(results, GrpcServiceConfigOrError.FromConfig(serviceConfig), GrpcAttributes.Empty));
            }
            catch (Exception ex)
            {
                observer.OnError(new Core.Status(Core.StatusCode.Unavailable, ex.Message));
            }
        }

        public void Dispose()
        {
            Unsubscribe();
            _timer.Dispose();
        }

        private GrpcHostAddress ParseARecord(IPAddress address, int port)
        {
            _logger.LogDebug($"Found a A record {address.ToString()}");
            return new GrpcHostAddress(address.ToString(), port);
        }
    }
}
