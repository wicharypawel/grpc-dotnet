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

using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Provider is responsible for creation of <seealso cref="IGrpcResolverPlugin"/>.
    /// </summary>
    public interface IGrpcResolverPluginProvider
    {
        /// <summary>
        /// Scheme used for target written eg. http, dns, xds etc.
        /// </summary>
        public string Scheme { get; }

        /// <summary>
        /// A priority, from 0 to 10 that this provider should be used, taking the current environment into 
        /// consideration. 5 should be considered the default, and then tweaked based on environment 
        /// detection. A priority of 0 does not imply that the provider wouldn't work; just that 
        /// it should be last in line.
        /// 
        /// Priority is there in case there would be two providers defined for single scheme. 
        /// It is usefull if we want to override default provider.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Whether this provider is available for use, taking the current environment into consideration.
        /// If false, no other methods are safe to be called.
        /// </summary>
        public bool IsAvailable { get; }

        /// <summary>
        /// Factory method.
        /// </summary>
        /// <param name="target">Target uri address. Uri scheme must match provider scheme.</param>
        /// <param name="attributes">Attributes for resolver plugin.</param>
        /// <returns>New instance of <seealso cref="IGrpcResolverPlugin"/>.</returns>
        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes);
    }
}
