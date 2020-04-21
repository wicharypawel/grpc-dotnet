using Moq;

namespace Grpc.Net.Client.LoadBalancing.Tests.Registries.Factories
{
    internal static class GrpcResolverPluginProviderFactory
    {
        public static IGrpcResolverPluginProvider GetProvider(string scheme, int priority, bool isAvailable)
        {
            var mock = new Mock<IGrpcResolverPluginProvider>(MockBehavior.Strict);
            mock.Setup(x => x.IsAvailable).Returns(isAvailable);
            mock.Setup(x => x.Priority).Returns(priority);
            mock.Setup(x => x.Scheme).Returns(scheme);
            return mock.Object;
        }
    }
}
