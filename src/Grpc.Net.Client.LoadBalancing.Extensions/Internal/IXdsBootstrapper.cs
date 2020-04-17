namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal interface IXdsBootstrapper
    {
        public XdsBootstrapInfo ReadBootstrap();
        public XdsBootstrapInfo ReadBootstrap(string inlineBootstrapFile);
    }
}
