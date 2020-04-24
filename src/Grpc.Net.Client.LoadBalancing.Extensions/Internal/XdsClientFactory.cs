using Microsoft.Extensions.Logging;
using System;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class XdsClientFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public XdsClientFactory(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _loggerFactory = loggerFactory;
        }

        internal IXdsClient? OverrideXdsClient { private get; set; }

        public IXdsClient CreateXdsClient()
        {
            if(OverrideXdsClient != null)
            {
                return OverrideXdsClient;
            }
            var bootstrapper = XdsBootstrapper.Instance;
            bootstrapper.LoggerFactory = _loggerFactory;
            return new XdsClient(bootstrapper, _loggerFactory);
        }
    }
}
