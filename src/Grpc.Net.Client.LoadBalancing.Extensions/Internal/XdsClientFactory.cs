namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class XdsClientFactory
    {
        public static IXdsClient CreateXdsClient()
        {
            return new XdsClient();
        }
    }
}
