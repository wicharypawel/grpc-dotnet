using Microsoft.Extensions.Logging;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class XdsClientFactory
    {
        internal static IXdsClient? OverrideXdsClient { private get; set; }

        public static IXdsClient CreateXdsClient(ILoggerFactory loggerFactory)
        {
            if(OverrideXdsClient != null)
            {
                return OverrideXdsClient;
            }
            var bootstrapper = XdsBootstrapper.Instance;
            bootstrapper.LoggerFactory = loggerFactory;
            return new XdsClient(bootstrapper);
        }
    }
}
