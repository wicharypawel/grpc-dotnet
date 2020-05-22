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

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Resolved address of server or lookaside load balancer.
    /// </summary>
    public sealed class GrpcHostAddress
    {
        /// <summary>
        /// Host address.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Port.
        /// </summary>
        public int? Port { get; set; } = null;

        /// <summary>
        /// Flag that indicate if machine is load balancer or service.
        /// </summary>
        public bool IsLoadBalancer { get; set; } = false;

        /// <summary>
        /// Priority value, which was obtained from SRV record, for this Host. Default value zero.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Weight value, which was obtained from SRV record, for this Host. Default value zero.
        /// </summary>
        public int Weight { get; set; } = 0;

        /// <summary>
        /// Creates a <see cref="GrpcHostAddress"/> with host and unassigned port.
        /// </summary>
        /// <param name="host">Host address of machine.</param>
        /// <param name="port">Machine port.</param>
        public GrpcHostAddress(string host, int? port = null)
        {
            Host = host;
            Port = port;
        }
    }
}
