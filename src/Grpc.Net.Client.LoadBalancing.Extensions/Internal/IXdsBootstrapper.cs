using Microsoft.Extensions.Logging;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal interface IXdsBootstrapper
    {
        ILoggerFactory LoggerFactory { set; }
        public XdsBootstrapInfo ReadBootstrap();
        public XdsBootstrapInfo ReadBootstrap(string inlineBootstrapFile);
    }
}
