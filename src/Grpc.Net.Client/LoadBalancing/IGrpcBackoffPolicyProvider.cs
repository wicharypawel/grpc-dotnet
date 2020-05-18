namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Provider is responsible for creation of <seealso cref="IGrpcBackoffPolicy"/>.
    /// </summary>
    internal interface IGrpcBackoffPolicyProvider
    {
        /// <summary>
        /// Factory method.
        /// </summary>
        /// <returns>New instance of <seealso cref="IGrpcBackoffPolicy"/>.</returns>
        public IGrpcBackoffPolicy CreateBackoffPolicy();
    }
}
