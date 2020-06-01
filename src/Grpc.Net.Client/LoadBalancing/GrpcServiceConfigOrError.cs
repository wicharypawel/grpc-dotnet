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

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Wraps parsed service config or an parsing error details.
    /// </summary>
    public sealed class GrpcServiceConfigOrError
    {
        /// <summary>
        /// Returns config if exists, otherwise null.
        /// </summary>
        public object? Config { get; }

        /// <summary>
        /// Returns error status if exists, otherwise null.
        /// </summary>
        public Status? Status { get; }

        private GrpcServiceConfigOrError(object? config, Status? status)
        {
            Config = config;
            Status = status;
        }

        /// <summary>
        /// Returns a <see cref="GrpcServiceConfigOrError"/> for the successfully parsed config.
        /// </summary>
        /// <param name="config">Parsed config.</param>
        /// <returns>Instance of <see cref="GrpcServiceConfigOrError"/>.</returns>
        public static GrpcServiceConfigOrError FromConfig(object config)
        {
            if (config == null)
            {
                throw new System.ArgumentNullException(nameof(config));
            }
            return new GrpcServiceConfigOrError(config, null);
        }

        /// <summary>
        /// Returns a <see cref="GrpcServiceConfigOrError"/> for the failure to parse the config.
        /// </summary>
        /// <param name="status">Parsing status error.</param>
        /// <returns>Instance of <see cref="GrpcServiceConfigOrError"/>.</returns>
        public static GrpcServiceConfigOrError FromError(Status status)
        {
            if (status.StatusCode == StatusCode.OK)
            {
                throw new System.ArgumentException($"Can not use OK {nameof(status)}.");
            }
            return new GrpcServiceConfigOrError(null, status);
        }
    }
}
