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
using System;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class NoOpResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => string.Empty;

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes)
        {
            return new NoOpResolverPlugin(attributes, TaskFactoryExecutor.Instance);
        }
    }

    /// <summary>
    /// Moreover, register NoOpResolverPlugin for http scheme. Dns discovery will be performed by HttpClient.
    /// </summary>
    internal sealed class HttpResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => "http";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes)
        {
            if (!target.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(nameof(target));
            }
            return new NoOpResolverPlugin(attributes, TaskFactoryExecutor.Instance);
        }
    }

    /// <summary>
    /// Moreover, register NoOpResolverPlugin for https scheme. Dns discovery will be performed by HttpClient.
    /// </summary>
    internal sealed class HttpsResolverPluginProvider : IGrpcResolverPluginProvider
    {
        public string Scheme => "https";

        public int Priority => 5;

        public bool IsAvailable => true;

        public IGrpcResolverPlugin CreateResolverPlugin(Uri target, GrpcAttributes attributes)
        {
            if (!target.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(nameof(target));
            }
            return new NoOpResolverPlugin(attributes, TaskFactoryExecutor.Instance);
        }
    }
}
