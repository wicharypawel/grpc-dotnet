#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using Grpc.Net.Client.LoadBalancing.Internal;
using Moq;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Providers
{
    public sealed class PickFirstPolicyProviderTests
    {
        [Fact]
        public void ForNewProvider_UsePickFirstPolicyProvider_VerifyBasicInformations()
        {
            // Arrange
            var provider = new PickFirstPolicyProvider();

            // Act
            // Assert
            Assert.Equal("pick_first", provider.PolicyName);
            Assert.Equal(5, provider.Priority);
            Assert.True(provider.IsAvailable);
        }

        [Fact]
        public void ForCreateLoadBalancingPolicy_UsePickFirstPolicyProvider_VerifyNewPolicyType()
        {
            // Arrange
            var provider = new PickFirstPolicyProvider();
            var helperMock = new Mock<IGrpcHelper>(MockBehavior.Strict);

            // Act
            using var policy = provider.CreateLoadBalancingPolicy(helperMock.Object);

            // Assert
            Assert.NotNull(policy);
            Assert.Equal(typeof(PickFirstPolicy), policy.GetType());
        }
    }
}
