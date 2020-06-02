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
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port) and a service config.
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// </summary>
    internal sealed class StaticResolverPlugin : IGrpcResolverPlugin
    {
        private readonly Func<Uri, GrpcNameResolutionResult> _staticNameResolution;
        private readonly IGrpcExecutor _executor;
        private readonly GrpcSynchronizationContext _synchronizationContext;
        private ILogger _logger = NullLogger.Instance;
        private Uri? _target = null;
        private IGrpcNameResolutionObserver? _observer = null;
        private bool _resolving = false;
        private bool _unsubscribed = false;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<StaticResolverPlugin>();
        }

        /// <summary>
        /// The ctor should only be called by <see cref="StaticResolverPluginProvider"/> or test code.
        /// </summary>
        internal StaticResolverPlugin(GrpcAttributes attributes, IGrpcExecutor executor)
        {
            StaticResolverPluginOptions? options = attributes.Get(GrpcAttributesConstants.StaticResolverOptions);
            _staticNameResolution = options?.StaticNameResolution ?? throw new ArgumentNullException(nameof(options.StaticNameResolution));
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
                _logger.LogDebug($"Using static name resolution");
                _logger.LogDebug($"Using static service config");
                observer.OnNext(_staticNameResolution(target));
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
