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
