using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Address of server that can handle requests for RPC.
    /// </summary>
    public sealed class GrpcSubChannel
    {
        /// <summary>
        /// Gets the server address in Uri form.
        /// </summary>
        public Uri Address { get; }

        /// <summary>
        /// LoadBalancer can use it to attach additional information.
        /// </summary>
        public GrpcAttributes Attributes { get; }

        /// <summary>
        /// Creates a <see cref="GrpcSubChannel"/> object with subchannel address.
        /// </summary>
        /// <param name="address">SubChannel address.</param>
        public GrpcSubChannel(Uri address) : this(address, GrpcAttributes.Empty)
        {
        }

        /// <summary>
        /// Creates a <see cref="GrpcSubChannel"/> object with subchannel address.
        /// </summary>
        /// <param name="address">SubChannel address.</param>
        /// <param name="attributes">Additional information for subchannel.</param>
        public GrpcSubChannel(Uri address, GrpcAttributes attributes)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Attributes = attributes ?? GrpcAttributes.Empty;
        }
    }
}
