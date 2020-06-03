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
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies
{
    public sealed class PickFirstPolicyPickersTests
    {
        [Fact]
        public void ForEmptySubchannel_UsePickFirstPolicyReadyPicker_Throws()
        {
            // Arrange
            // Act
            // Assert  
            Assert.Throws<ArgumentNullException>(() =>
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                var _ = new PickFirstPolicy.ReadyPicker(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            });
        }

        [Fact]
        public void ForGrpcSubChannels_UsePickFirstPolicyReadyPicker_SelectFirstChannel()
        {
            // Arrange
            var subChannels = GrpcSubChannelFactory.GetSubChannels();
            var picker = new PickFirstPolicy.ReadyPicker(subChannels[0]);

            // Act
            // Assert
            for (int i = 0; i < 30; i++)
            {
                var pickResult = picker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty);
                Assert.NotNull(pickResult);
                Assert.NotNull(pickResult!.SubChannel);
                Assert.Equal(subChannels[0].Address.Host, pickResult!.SubChannel!.Address.Host);
                Assert.Equal(subChannels[0].Address.Port, pickResult.SubChannel.Address.Port);
                Assert.Equal(subChannels[0].Address.Scheme, pickResult.SubChannel.Address.Scheme);
                Assert.Equal(StatusCode.OK, pickResult.Status.StatusCode);
                Assert.False(pickResult.Drop);
            }
        }

        [Fact]
        public void ForEqualityTestAndEqualValues_UsePickFirstPolicyReadyPicker_AssertTrue()
        {
            // Arrange
            var subChannel = GrpcSubChannelFactory.GetSubChannels()[0];
            var picker = new PickFirstPolicy.ReadyPicker(subChannel);
            var pickerOther = new PickFirstPolicy.ReadyPicker(subChannel);

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.True(areEqual);
            Assert.True(picker.IsEquivalentTo(picker));
        }

        [Fact]
        public void ForEqualityTestAndDifferentTypes_UsePickFirstPolicyReadyPicker_AssertFalse()
        {
            // Arrange
            var subChannel = GrpcSubChannelFactory.GetSubChannels()[0];
            var picker = new PickFirstPolicy.ReadyPicker(subChannel);
            var pickerOther = new PickFirstPolicy.EmptyPicker(Status.DefaultSuccess);

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.False(areEqual);
        }

        [Fact]
        public void ForEqualityTestAndDifferentElementsCollections_UsePickFirstPolicyReadyPicker_AssertFalse()
        {
            // Arrange
            var subChannel = GrpcSubChannelFactory.GetSubChannels()[0];
            var picker = new PickFirstPolicy.ReadyPicker(subChannel);
            var subChannelOther = GrpcSubChannelFactory.GetSubChannels()[1];
            var pickerOther = new PickFirstPolicy.ReadyPicker(subChannelOther);

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.False(areEqual);
        }

        [Fact]
        public void ForInternalStatus_UsePickFirstPolicyEmptyPicker_VerifyErrorPickResult()
        {
            // Arrange
            var picker = new PickFirstPolicy.EmptyPicker(new Status(StatusCode.Internal, "test bug"));

            // Act
            var pickResult = picker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty);

            // Assert
            Assert.NotNull(pickResult);
            Assert.Equal(StatusCode.Internal, pickResult.Status.StatusCode);
            Assert.Null(pickResult.SubChannel);
            Assert.False(pickResult.Drop);
        }

        [Fact]
        public void ForOkStatus_UsePickFirstPolicyEmptyPicker_VerifyOkPickResult()
        {
            // Arrange
            var picker = new PickFirstPolicy.EmptyPicker(new Status(StatusCode.OK, string.Empty));

            // Act
            var pickResult = picker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty);

            // Assert
            Assert.NotNull(pickResult);
            Assert.Equal(StatusCode.OK, pickResult.Status.StatusCode);
            Assert.Null(pickResult.SubChannel);
            Assert.False(pickResult.Drop);
        }

        [Fact]
        public void ForEqualityTestAndEqualValues_UsePickFirstPolicyEmptyPicker_AssertTrue()
        {
            // Arrange
            var picker = new PickFirstPolicy.EmptyPicker(new Status(StatusCode.OK, string.Empty));
            var pickerOther = new PickFirstPolicy.EmptyPicker(new Status(StatusCode.OK, string.Empty));

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.True(areEqual);
            Assert.True(picker.IsEquivalentTo(picker));
        }

        [Fact]
        public void ForEqualityTestAndDifferentTypes_UsePickFirstPolicyEmptyPicker_AssertFalse()
        {
            // Arrange
            var picker = new PickFirstPolicy.EmptyPicker(new Status(StatusCode.OK, string.Empty));
            var pickerOther = new PickFirstPolicy.ReadyPicker(GrpcSubChannelFactory.GetSubChannels()[0]);

            // Act
            var areEqual = picker.IsEquivalentTo(pickerOther);

            // Assert
            Assert.False(areEqual);
        }
    }
}
