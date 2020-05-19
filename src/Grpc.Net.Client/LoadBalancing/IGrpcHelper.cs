using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Provides essentials for LoadBalancer implementations.
    /// </summary>
    public interface IGrpcHelper
    {
        /// <summary>
        /// Creates a Subchannel, which is a logical connection to the given address.
        /// 
        /// The LoadBalancer is responsible for closing unused Subchannels, and closing all
        /// Subchannels within disposal.
        /// 
        /// It must be called from <see cref="GrpcSynchronizationContext"/>.
        /// </summary>
        /// <param name="arguments">The arguments are custom attributes associated with this Subchannel.</param>
        /// <returns></returns>
        public IGrpcSubChannel CreateSubChannel(CreateSubchannelArgs arguments);

        /// <summary>
        /// Set a new state with a new picker to the channel.
        /// 
        /// The channel will hold the picker and use it for all RPCs, until
        /// <see cref="UpdateBalancingState"/> is called again and a new picker replaces the old one.
        /// If <see cref="UpdateBalancingState"/> has never been called, the channel will buffer all RPCs until a
        /// picker is provided.
        /// 
        /// The passed state will be the channel's new state. The SHUTDOWN state should not be passed
        /// and its behavior is undefined.
        /// 
        /// It must be called from <see cref="GrpcSynchronizationContext"/>.
        /// </summary>
        /// <param name="newState">The channel's new state.</param>
        /// <param name="newPicker">The channel's new picker.</param>
        public void UpdateBalancingState(GrpcConnectivityState newState, IGrpcSubChannelPicker newPicker);

        /// <summary>
        /// Call refresh on the channel's resolver.
        /// 
        /// It must be called from <see cref="GrpcSynchronizationContext"/>.
        /// </summary>
        public void RefreshNameResolution();

        /// <summary>
        /// Returns a <see cref="GrpcSynchronizationContext"/> that runs tasks in the same Synchronization Context.
        /// It allows ordering tasks execution in order to prevent races on shared state.
        /// </summary>
        /// <returns>An instance of context.</returns>
        public GrpcSynchronizationContext GetSynchronizationContext();
    }

    /// <summary>
    /// Arguments for creating a <see cref="IGrpcSubChannel"/>.
    /// </summary>
    public sealed class CreateSubchannelArgs
    {
        /// <summary>
        /// The Subchannel's address.
        /// </summary>
        public Uri Address { get; }

        /// <summary>
        /// The Subchannel's attributes.
        /// </summary>
        public GrpcAttributes Attributes { get; }

        /// <summary>
        /// Creates a new instance of <see cref="CreateSubchannelArgs"/>.
        /// </summary>
        /// <param name="address">The Subchannel's address.</param>
        /// <param name="attributes">The Subchannel's attributes.</param>
        public CreateSubchannelArgs(Uri address, GrpcAttributes attributes)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }
    }
}
