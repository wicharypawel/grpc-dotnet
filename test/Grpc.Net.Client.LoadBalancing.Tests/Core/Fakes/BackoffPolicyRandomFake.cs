namespace Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes
{
    internal sealed class BackoffPolicyRandomFake : GrpcExponentialBackoffPolicy.IRandom
    {
        public double NextValue { get; set; } = 0.5;

        public double NextDouble()
        {
            return NextValue;
        }
    }
}
