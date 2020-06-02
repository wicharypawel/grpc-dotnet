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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// No-Op resolver will pass uri address to HttpClient to handle.
    /// </summary>
    internal sealed class NoOpResolverPlugin : IGrpcResolverPlugin
    {
        private readonly string[] WellKnownSchemes = new string[] { "dns", "xds", "xds-experimental" };
        private readonly string _defaultLoadBalancingPolicy;
        private readonly IGrpcExecutor _executor;
        private readonly GrpcSynchronizationContext _synchronizationContext;
        private ILogger _logger = NullLogger.Instance;
        private Uri? _target = null;
        private IGrpcNameResolutionObserver? _observer = null;
        private bool _resolving = false;
        private bool _unsubscribed = false;

        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<NoOpResolverPlugin>();
        }

        /// <summary>
        /// The ctor should only be called by <see cref="NoOpResolverPluginProvider"/> or test code.
        /// </summary>
        internal NoOpResolverPlugin(GrpcAttributes attributes, IGrpcExecutor executor)
        {
            _defaultLoadBalancingPolicy = attributes.Get(GrpcAttributesConstants.DefaultLoadBalancingPolicy) ?? "pick_first";
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
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
        }

        public void Unsubscribe()
        {
            _unsubscribed = true;
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
            if (_resolving || _unsubscribed)
            {
                return;
            }
            _resolving = true;
            var target = _target ?? throw new ArgumentNullException(nameof(_target));
            var observer = _observer ?? throw new ArgumentNullException(nameof(_observer));
            _executor.Execute(async () => await ResolveCoreAsync(target, observer).ConfigureAwait(false));
        }

        private Task ResolveCoreAsync(Uri target, IGrpcNameResolutionObserver observer)
        {
            try
            {
                if (WellKnownSchemes.Contains(target.Scheme.ToLowerInvariant()))
                {
                    observer.OnError(new Status(StatusCode.Unavailable, $"{target.Scheme}:// scheme require non-default name resolver."));
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
            catch (Exception ex)
            {
                observer.OnError(new Status(StatusCode.Unavailable, ex.Message));
                return Task.CompletedTask;
            }
            finally
            {
                _synchronizationContext.Execute(() => { _resolving = false; });
            }
        }

        public void Dispose()
        {
            Unsubscribe();
        }
    }
}
