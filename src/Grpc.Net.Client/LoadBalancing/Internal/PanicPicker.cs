using Grpc.Core;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class PanicPicker : IGrpcSubChannelPicker
    {
        private readonly GrpcPickResult _panicPickResult = GrpcPickResult.WithDrop(new Status(StatusCode.Internal, "Panic! This is a bug!"));

        public GrpcPickResult GetNextSubChannel(IGrpcPickSubchannelArgs arguments)
        {
            return _panicPickResult;
        }

        public void Dispose()
        {
        }
    }
}
