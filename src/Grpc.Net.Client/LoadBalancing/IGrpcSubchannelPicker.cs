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
    /// Picker does the actual load-balancing work. It selects a <see cref="GrpcPickResult"/> for each new RPC.
    /// </summary>
    public interface IGrpcSubChannelPicker
    {
        /// <summary>
        /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
        /// </summary>
        /// <returns>A balancing decision for a new RPC.</returns>
        public GrpcPickResult GetNextSubChannel(IGrpcPickSubchannelArgs arguments);
    }

    /// <summary>
    /// Provides arguments for a <see cref="IGrpcSubChannelPicker.GetNextSubChannel"/>.
    /// 
    /// Interface is designed for future development.
    /// </summary>
    public interface IGrpcPickSubchannelArgs
    {
    }

    /// <summary>
    /// Provides arguments for a <see cref="IGrpcSubChannelPicker.GetNextSubChannel"/>. 
    /// 
    /// Class is designed for future development.
    /// </summary>
    public sealed class GrpcPickSubchannelArgs : IGrpcPickSubchannelArgs
    {
        /// <summary>
        /// Creates new instance of <see cref="GrpcPickSubchannelArgs"/>.
        /// </summary>
        private GrpcPickSubchannelArgs()
        {
        }

        /// <summary>
        /// Returns instance of <see cref="GrpcPickSubchannelArgs"/> with default values.
        /// </summary>
        public static GrpcPickSubchannelArgs Empty { get; } = new GrpcPickSubchannelArgs();
    }
}
