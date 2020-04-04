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
        /// Gets LoadBalanceToken value. Token should be included into the initial metadata when client 
        /// starts a call to that server. The token is used by the server to report load to the gRPC LB system.
        /// If token is not present, string.Empty value is returned.
        /// </summary>
        public string LoadBalanceToken { get; }

        /// <summary>
        /// Creates a <see cref="GrpcSubChannel"/> object with subchannel address.
        /// </summary>
        /// <param name="address">SubChannel address</param>
        public GrpcSubChannel(Uri address) : this(address, string.Empty)
        {
        }

        /// <summary>
        /// Creates a <see cref="GrpcSubChannel"/> object with subchannel address.
        /// </summary>
        /// <param name="address">SubChannel address</param>
        /// <param name="loadBalanceToken">LoadBalanceToken for subchannel</param>
        public GrpcSubChannel(Uri address, string loadBalanceToken)
        {
            Address = address;
            LoadBalanceToken = loadBalanceToken ?? string.Empty;
        }
    }
}
