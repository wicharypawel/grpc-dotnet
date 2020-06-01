﻿#region Copyright notice and license

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
