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

using Grpc.Core;
using Grpc.Net.Client.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Net;
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
        private readonly string _defaultLoadBalancingPolicy;
        private readonly double? _periodicResolutionSeconds;
        private readonly IGrpcExecutor _executor;
        private readonly ITimer _timer;
        private readonly GrpcSynchronizationContext _synchronizationContext;
        private ILogger _logger = NullLogger.Instance;
        private Uri? _target = null;
        private IGrpcNameResolutionObserver? _observer = null;
        private bool _resolving = false;
        private bool _shutdown = false;

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
        /// The ctor should only be called by <see cref="DnsResolverPluginProvider"/> or test code.
        /// </summary>
        internal DnsResolverPlugin(GrpcAttributes attributes, IGrpcExecutor executor, ITimer timer)
        {
            _defaultLoadBalancingPolicy = attributes.Get(GrpcAttributesConstants.DefaultLoadBalancingPolicy) ?? "pick_first";
            _periodicResolutionSeconds = attributes.GetValue(GrpcAttributesConstants.DnsResolverPeriodicResolutionSeconds);
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _synchronizationContext = attributes.Get(GrpcAttributesConstants.ChannelSynchronizationContext)
                ?? throw new ArgumentNullException($"Missing synchronization context in {nameof(attributes)}");
        }

        public void Subscribe(Uri target, IGrpcNameResolutionObserver observer)
        {
            if (_observer != null)
            {
                throw new InvalidOperationException("Observer already registered.");
            }
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            Resolve();
            if (_periodicResolutionSeconds.HasValue)
            {
                _timer.Start((state) => { RefreshResolution(); }, null,
                    TimeSpan.FromSeconds(_periodicResolutionSeconds.Value), TimeSpan.FromSeconds(_periodicResolutionSeconds.Value));
            }
        }

        public void Shutdown()
        {
            if (_shutdown)
            {
                return;
            }
            _shutdown = true;
            _timer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            _observer = null;
            _target = null;
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
            if (_resolving || _shutdown)
            {
                return;
            }
            _resolving = true;
            var target = _target ?? throw new ArgumentNullException(nameof(_target));
            var observer = _observer ?? throw new ArgumentNullException(nameof(_observer));
            _executor.Execute(async () => await ResolveCoreAsync(target, observer).ConfigureAwait(false));
        }

        private async Task ResolveCoreAsync(Uri target, IGrpcNameResolutionObserver observer)
        {
            try
            {
                if (!target.Scheme.Equals("dns", StringComparison.OrdinalIgnoreCase))
                {
                    observer.OnError(new Status(StatusCode.Unavailable, $"{nameof(DnsResolverPlugin)} require dns:// scheme to set as target address."));
                    return;
                }
                _logger.LogDebug($"Start A lookup for {target.Host}");
                var serversDnsQueryTask = OverrideDnsResults ?? Dns.GetHostAddressesAsync(target.Host);
                var serversDnsQueryResults = await serversDnsQueryTask.ConfigureAwait(false);
                var results = serversDnsQueryResults.Select(x => ParseARecord(x, target.Port)).ToList();
                _logger.LogDebug($"NameResolution found {results.Count} DNS records");
                var serviceConfig = GrpcServiceConfig.Create(_defaultLoadBalancingPolicy);
                _logger.LogDebug($"Service config created with policies: {string.Join(',', serviceConfig.RequestedLoadBalancingPolicies)}");
                observer.OnNext(new GrpcNameResolutionResult(results, GrpcServiceConfigOrError.FromConfig(serviceConfig), GrpcAttributes.Empty));
            }
            catch (Exception ex)
            {
                observer.OnError(new Status(StatusCode.Unavailable, ex.Message));
            }
            finally
            {
                _synchronizationContext.Execute(() => { _resolving = false; });
            }
        }

        public void Dispose()
        {
            Shutdown();
            _timer.Dispose();
        }

        private GrpcHostAddress ParseARecord(IPAddress address, int port)
        {
            _logger.LogDebug($"Found a A record {address.ToString()}");
            return new GrpcHostAddress(address.ToString(), port);
        }
    }
}
