namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class XdsClientFactory
    {
        internal static IXdsClient? OverrideXdsClient { private get; set; }

        public static IXdsClient CreateXdsClient()
        {
            if(OverrideXdsClient != null)
            {
                return OverrideXdsClient;
            }
            var bootstrapper = XdsBootstrapper.Instance;
            return new XdsClient(bootstrapper);
        }
    }
}
