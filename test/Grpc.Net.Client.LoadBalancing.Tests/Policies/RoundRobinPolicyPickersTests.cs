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

using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using System;
using System.Collections.Generic;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies
{
    public sealed class RoundRobinPolicyPickersTests
    {
        [Fact]
        public void ForEmptySubchannels_UseRoundRobinPolicyReadyPicker_Throws()
        {
            // Arrange
            // Act
            // Assert  
            Assert.Throws<ArgumentException>(() => 
            {
                var _ = new RoundRobinPolicy.ReadyPicker(new List<IGrpcSubChannel>());
            });
        }

        [Fact]
        public void ForGrpcSubChannels_UseRoundRobinPolicyReadyPicker_SelectChannelsInRoundRobin()
        {
            // Arrange
            var subChannels = GrpcSubChannelFactory.GetSubChannels();
            var picker = new RoundRobinPolicy.ReadyPicker(subChannels);

            // Act
            // Assert
            for (int i = 0; i < 30; i++)
            {
                var pickResult = picker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty);
                Assert.NotNull(pickResult);
                Assert.NotNull(pickResult!.SubChannel);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Host, pickResult!.SubChannel!.Address.Host);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Port, pickResult.SubChannel.Address.Port);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Scheme, pickResult.SubChannel.Address.Scheme);
                Assert.Equal(StatusCode.OK, pickResult.Status.StatusCode);
                Assert.False(pickResult.Drop);
            }
        }

        [Fact]
        public void ForEqualityTestAndEqualValues_UseRoundRobinPolicyReadyPicker_AssertTrue()
        {
            // Arrange
            var subChannels = GrpcSubChannelFactory.GetSubChannels();
            var picker = new RoundRobinPolicy.ReadyPicker(subChannels);
            var pickerOther = new RoundRobinPolicy.ReadyPicker(subChannels);

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.True(areEqual);
            Assert.True(picker.IsEquivalentTo(picker));
        }

        [Fact]
        public void ForEqualityTestAndDifferentTypes_UseRoundRobinPolicyReadyPicker_AssertFalse()
        {
            // Arrange
            var subChannels = GrpcSubChannelFactory.GetSubChannels();
            var picker = new RoundRobinPolicy.ReadyPicker(subChannels);
            var pickerOther = new RoundRobinPolicy.EmptyPicker(new Status(StatusCode.Internal, "test bug"));

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.False(areEqual);
        }

        [Fact]
        public void ForEqualityTestAndNonEqualCountCollections_UseRoundRobinPolicyReadyPicker_AssertFalse()
        {
            // Arrange
            var subChannels = GrpcSubChannelFactory.GetSubChannels();
            var picker = new RoundRobinPolicy.ReadyPicker(subChannels);
            var subChannelsOther = GrpcSubChannelFactory.GetSubChannels(2);
            var pickerOther = new RoundRobinPolicy.ReadyPicker(subChannelsOther);

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.False(areEqual);
        }

        [Fact]
        public void ForEqualityTestAndDifferentElementsCollections_UseRoundRobinPolicyReadyPicker_AssertFalse()
        {
            // Arrange
            var subChannels = GrpcSubChannelFactory.GetSubChannels(3);
            var picker = new RoundRobinPolicy.ReadyPicker(subChannels);
            var subChannelsOther = GrpcSubChannelFactory.GetSubChannels(3);
            var pickerOther = new RoundRobinPolicy.ReadyPicker(subChannelsOther);

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.False(areEqual);
        }

        [Fact]
        public void ForInternalStatus_UseRoundRobinPolicyEmptyPicker_VerifyErrorPickResult()
        {
            // Arrange
            var picker = new RoundRobinPolicy.EmptyPicker(new Status(StatusCode.Internal, "test bug"));

            // Act
            var pickResult = picker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty);

            // Assert
            Assert.NotNull(pickResult);
            Assert.Equal(StatusCode.Internal, pickResult.Status.StatusCode);
            Assert.Null(pickResult.SubChannel);
            Assert.False(pickResult.Drop);
        }

        [Fact]
        public void ForOkStatus_UseRoundRobinPolicyEmptyPicker_VerifyOkPickResult()
        {
            // Arrange
            var picker = new RoundRobinPolicy.EmptyPicker(new Status(StatusCode.OK, string.Empty));

            // Act
            var pickResult = picker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty);

            // Assert
            Assert.NotNull(pickResult);
            Assert.Equal(StatusCode.OK, pickResult.Status.StatusCode);
            Assert.Null(pickResult.SubChannel);
            Assert.False(pickResult.Drop);
        }

        [Fact]
        public void ForEqualityTestAndEqualValues_UseRoundRobinPolicyEmptyPicker_AssertTrue()
        {
            // Arrange
            var picker = new RoundRobinPolicy.EmptyPicker(new Status(StatusCode.OK, string.Empty));
            var pickerOther = new RoundRobinPolicy.EmptyPicker(new Status(StatusCode.OK, string.Empty));

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.True(areEqual);
            Assert.True(picker.IsEquivalentTo(picker));
        }

        [Fact]
        public void ForEqualityTestAndDifferentTypes_UseRoundRobinPolicyEmptyPicker_AssertFalse()
        {
            // Arrange
            var picker = new RoundRobinPolicy.EmptyPicker(new Status(StatusCode.OK, string.Empty));
            var pickerOther = new RoundRobinPolicy.ReadyPicker(GrpcSubChannelFactory.GetSubChannels(2));

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.False(areEqual);
        }
    }
}
