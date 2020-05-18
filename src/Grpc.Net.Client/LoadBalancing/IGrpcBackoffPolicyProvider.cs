namespace Grpc.Net.Client.LoadBalancing
{
    internal interface IGrpcBackoffPolicyProvider
    {
        public IGrpcBackoffPolicy CreateBackoffPolicy();
    }
}
