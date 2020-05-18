using Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes;
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcExponentialBackoffPolicyTests
    {
        [Fact]
        public void ForNextBackoff_UsingGrpcExponentialBackoffPolicy_ReturnsNotZero()
        {
            // Arrange
            var policy = new GrpcExponentialBackoffPolicy(new BackoffPolicyRandomFake(), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2), 1.6, 0.2);

            // Act
            var nextBackoff = policy.NextBackoff();

            // Assert
            Assert.NotEqual(TimeSpan.Zero, nextBackoff);
        }

        [Fact]
        public void ForNextBackoffs_UsingGrpcExponentialBackoffPolicy_VerifyIfAscending()
        {
            // Arrange
            var policy = new GrpcExponentialBackoffPolicy(new BackoffPolicyRandomFake(), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2), 1.6, 0.2);

            // Act
            // Assert
            var previousValue = policy.NextBackoff();
            for (int i = 0; i < 5; i++) //do not increase loop upper limit as you may face maxBackoff
            {
                var currentValue = policy.NextBackoff();
                Assert.True(previousValue < currentValue);
                previousValue = currentValue;
            }
        }

        [Fact]
        public void ForNextBackoffs_UsingGrpcExponentialBackoffPolicy_VerifyBackoffParameters()
        {
            // Arrange
            var initialBackoff = TimeSpan.FromSeconds(1);
            var multiplier = 1.6;
            var policy = new GrpcExponentialBackoffPolicy(new BackoffPolicyRandomFake(), initialBackoff, TimeSpan.FromMinutes(2), multiplier, 0);

            // Act
            var nextBackoff = policy.NextBackoff();
            Assert.True(nextBackoff == initialBackoff); // jitter must be set zero to perform strict equal assertion
            nextBackoff = policy.NextBackoff();
            Assert.True(nextBackoff == initialBackoff * multiplier); // jitter must be set zero to perform strict equal assertion

            // Assert
            Assert.True(policy.NextBackoff() == nextBackoff * multiplier); // jitter must be set zero to perform strict equal assertion
        }

        [Fact]
        public void ForNextBackoffWithReachedMaxDelay_UsingGrpcExponentialBackoffPolicy_ReturnsMaxDelay()
        {
            // Arrange
            var maxBackoff = TimeSpan.FromMinutes(2);
            var policy = new GrpcExponentialBackoffPolicy(new BackoffPolicyRandomFake(), TimeSpan.FromSeconds(1), maxBackoff, 1.6, 0); 

            // Act
            for (int i = 0; i < 50; i++)
            {
                if (policy.NextBackoff() >= maxBackoff)
                {
                    break; 
                }
            }

            // Assert
            Assert.Equal(maxBackoff, policy.NextBackoff()); // jitter must be set zero to perform strict equal assertion
        }
    }
}
