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
    /// A logical connection to a server, or a group of equivalent servers.
    /// 
    /// It maintains a physical connection (aka transport) for sending new RPCs.
    /// 
    /// <see cref="Start"/> method must be called prior to calling any other methods, 
    /// with the exception of <see cref="Shutdown"/>, which can be called at any time.
    /// </summary>
    public interface IGrpcSubChannel
    {
        /// <summary>
        /// Starts the Subchannel. Can only be called once.
        /// 
        /// Must be called prior to any other method on this class, except for
        /// <see cref="Shutdown"/>, which can be called at any time.
        /// 
        /// Must be called from the <see cref="IGrpcHelper.GetSynchronizationContext"/>
        /// otherwise it may throw. 
        /// </summary>
        /// <param name="observer">Observer receives state updates for this Subchannel.</param>
        public void Start(IGrpcSubchannelStateObserver observer);

        /// <summary>
        /// Shuts down the Subchannel. After this method is called, this Subchannel should no longer
        /// be returned by the latest <see cref="IGrpcSubChannelPicker"/>, and can be safely discarded.
        /// 
        /// Calling it on an already shut-down Subchannel has no effect. It should be called from the Synchronization Context.
        /// </summary>
        public void Shutdown();

        /// <summary>
        /// Asks the Subchannel to create a connection (aka transport).
        /// 
        /// It should be called from the Synchronization Context.
        /// </summary>
        public void RequestConnection();

        /// <summary>
        /// Gets the server address in Uri form.
        /// </summary>
        public Uri Address { get; }

        /// <summary>
        /// LoadBalancer can use it to attach additional information.
        /// </summary>
        public GrpcAttributes Attributes { get; }
    }
}
