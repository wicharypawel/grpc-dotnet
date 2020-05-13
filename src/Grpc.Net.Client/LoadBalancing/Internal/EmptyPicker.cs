namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// This class is used until delayed transport will be added.
    /// </summary>
    internal sealed class EmptyPicker : IGrpcSubChannelPicker
    {
        public GrpcPickResult GetNextSubChannel()
        {
            return GrpcPickResult.WithNoResult();
        }

        public void Dispose()
        {
        }
    }
}
