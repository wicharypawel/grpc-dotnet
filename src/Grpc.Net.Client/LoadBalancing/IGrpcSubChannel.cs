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

        /// <summary>
        /// Replaces the existing addresses used with this <see cref="IGrpcSubChannel"/>. If the new and old
        /// addresses overlap, the Subchannel can continue using an existing connection.
        /// It must be called from the Synchronization Context or will throw.
        /// </summary>
        public void UpdateAddress(Uri address);
    }
}
