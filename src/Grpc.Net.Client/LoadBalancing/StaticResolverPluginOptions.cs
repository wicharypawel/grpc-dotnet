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

using Grpc.Net.Client.LoadBalancing.Internal;
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// An options class for configuring a <see cref="StaticResolverPlugin"/>.
    /// </summary>
    public sealed class StaticResolverPluginOptions
    {
        /// <summary>
        /// Define name resolution using lambda.
        /// </summary>
        public Func<Uri, GrpcNameResolutionResult> StaticNameResolution { get; set; }

        /// <summary>
        /// Creates a new instance of <seealso cref="StaticResolverPluginOptions"/>.
        /// </summary>
        /// <param name="staticNameResolution">Define name resolution using lambda.</param>
        public StaticResolverPluginOptions(Func<Uri, GrpcNameResolutionResult> staticNameResolution)
        {
            StaticNameResolution = staticNameResolution;
        }
    }
}
