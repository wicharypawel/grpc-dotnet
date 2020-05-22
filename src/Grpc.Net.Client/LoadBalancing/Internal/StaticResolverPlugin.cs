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
using System.Threading;
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
        private ILogger _logger = NullLogger.Instance;
        private Uri? _target = null;
        private IGrpcNameResolutionObserver? _observer = null;
        private CancellationTokenSource? _cancellationTokenSource = null;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<StaticResolverPlugin>();
        }

        /// <summary>
        /// Creates a <seealso cref="StaticResolverPlugin"/> using specified <seealso cref="GrpcAttributes"/>.
        /// </summary>
        /// <param name="attributes">Attributes with options.</param>
        public StaticResolverPlugin(GrpcAttributes attributes)
        {
            var options = attributes.Get(GrpcAttributesConstants.StaticResolverOptions) as StaticResolverPluginOptions;
            _staticNameResolution = options?.StaticNameResolution ?? throw new ArgumentNullException(nameof(options.StaticNameResolution));
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
            _logger.LogDebug($"Using static name resolution");
            _logger.LogDebug($"Using static service config");
            observer.OnNext(_staticNameResolution(target));
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Unsubscribe();
        }
    }
}
