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
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port) and a service config.
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// </summary>
    public interface IGrpcResolverPlugin : IDisposable
    {
        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory { set; }

        /// <summary>
        /// Starts the resolution.
        /// </summary>
        /// <param name="target">Server address with scheme.</param>
        /// <param name="observer">Observer used to receive updates on the target.</param>
        public void Subscribe(Uri target, IGrpcNameResolutionObserver observer);

        /// <summary>
        /// Stops the resolution. Updates to the Listener will stop.
        /// </summary>
        public void Unsubscribe();

        /// <summary>
        /// Re-resolve the name. Can only be called after Start method has been called.
        /// This is only a hint. Implementation takes it as a signal but may not start resolution 
        /// immediately. It should never throw.
        /// 
        /// It is possible to leave this operation empty (no-op). 
        /// </summary>
        public void RefreshResolution();
    }
}
