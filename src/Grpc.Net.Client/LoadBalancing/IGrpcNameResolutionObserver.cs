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
    /// An interface for objects that react to changes of <seealso cref="GrpcNameResolutionResult"/>.
    /// Receives address updates. All methods are expected to return quickly.
    /// </summary>
    public interface IGrpcNameResolutionObserver
    {
        /// <summary>
        /// Handles updates on resolved addresses and attributes.
        /// </summary>
        /// <param name="value">ResolutionResult the resolved server addresses, attributes, and Service Config.</param>
        public void OnNext(GrpcNameResolutionResult value);

        /// <summary>
        /// Handles a name resolving error from the resolver. The observer is responsible for eventually
        /// invoking Refresh method to re-attempt resolution.
        /// </summary>
        /// <param name="error">Error a non-OK status.</param>
        public void OnError(Status error);
    }
}
