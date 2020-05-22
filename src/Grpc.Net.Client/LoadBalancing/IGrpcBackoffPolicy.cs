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
    /// Determines how long to wait before doing some action (typically a retry, or a reconnect).
    /// </summary>
    internal interface IGrpcBackoffPolicy
    {
        /// <summary>
        /// Returns ascending values of waiting time. In order to start from initial value, create new policy.
        /// </summary>
        /// <returns>Wait time.</returns>
        public TimeSpan NextBackoff();
    }
}
