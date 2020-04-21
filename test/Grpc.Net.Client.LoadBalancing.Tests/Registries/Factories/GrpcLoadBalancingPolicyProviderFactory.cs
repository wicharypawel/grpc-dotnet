using Moq;

namespace Grpc.Net.Client.LoadBalancing.Tests.Registries.Factories
{
    internal static class GrpcLoadBalancingPolicyProviderFactory
    {
        public static IGrpcLoadBalancingPolicyProvider GetProvider(string policyName, int priority, bool isAvailable)
        {
            var mock = new Mock<IGrpcLoadBalancingPolicyProvider>(MockBehavior.Strict);
            mock.Setup(x => x.IsAvailable).Returns(isAvailable);
            mock.Setup(x => x.Priority).Returns(priority);
            mock.Setup(x => x.PolicyName).Returns(policyName);
            return mock.Object;
        }
    }
}
