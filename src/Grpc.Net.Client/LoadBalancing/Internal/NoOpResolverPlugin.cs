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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// No-Op resolver will pass uri address to HttpClient to handle.
    /// </summary>
    internal sealed class NoOpResolverPlugin : IGrpcResolverPlugin
    {
        private readonly string[] WellKnownSchemes = new string[] { "dns", "xds", "xds-experimental" }; 
        private ILogger _logger = NullLogger.Instance;
        private readonly string _defaultLoadBalancingPolicy;
        private Uri? _target = null;
        private IGrpcNameResolutionObserver? _observer = null;
        private CancellationTokenSource? _cancellationTokenSource = null;

        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<NoOpResolverPlugin>();
        }

        public NoOpResolverPlugin()
        {
            _defaultLoadBalancingPolicy = "pick_first";
        }

        public NoOpResolverPlugin(GrpcAttributes attributes)
        {
            _defaultLoadBalancingPolicy = attributes.Get(GrpcAttributesConstants.DefaultLoadBalancingPolicy) ?? "pick_first";
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
            Resolve();
        }

        public void Unsubscribe()
        {
            _observer = null;
            _target = null;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
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

        private Task ResolveCoreAsync(Uri? target, IGrpcNameResolutionObserver? observer)
        {
            if (observer == null)
            {
                return Task.CompletedTask;
            }
            if (target == null)
            {
                observer.OnError(new Core.Status(Core.StatusCode.Unavailable, "Target is empty."));
                return Task.CompletedTask;
            }
            if (WellKnownSchemes.Contains(target.Scheme.ToLowerInvariant()))
            {
                observer.OnError(new Core.Status(Core.StatusCode.Unavailable, $"{target.Scheme}:// scheme require non-default name resolver."));
                return Task.CompletedTask;
            }
            _logger.LogDebug("NoOpResolverPlugin using defined target as name resolution");
            var hosts = new List<GrpcHostAddress>()
            {
               new GrpcHostAddress(target.Host, target.Port)
            };
            _logger.LogDebug($"NoOpResolverPlugin returns {_defaultLoadBalancingPolicy} policy");
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create(_defaultLoadBalancingPolicy));
            observer.OnNext(new GrpcNameResolutionResult(hosts, config, GrpcAttributes.Empty));
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Unsubscribe();
        }
    }
}
