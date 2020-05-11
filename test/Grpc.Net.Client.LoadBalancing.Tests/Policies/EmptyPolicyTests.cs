using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Internal;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies
{
    public sealed class EmptyPolicyTests
    {
        [Fact]
        public void ForGrpcSubChannels_UseEmptyPolicySelectChannels_SelectNoResult()
        {
            // Arrange
            // Act
            using var policy = new EmptyPolicy();
            var pickResult = policy.GetNextSubChannel();

            // Assert
            Assert.NotNull(pickResult);
            Assert.Null(pickResult.SubChannel);
            Assert.Equal(Status.DefaultSuccess, pickResult.Status);
        }
    }
}
