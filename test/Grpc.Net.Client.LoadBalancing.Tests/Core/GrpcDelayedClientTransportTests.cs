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
using Grpc.Net.Client.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcDelayedClientTransportTests
    {
        [Fact]
        public void ForNewInstance_UsingGrpcDelayedClientTransport_VerifyNoWaitingCalls()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);

            // Act
            // Assert
            Assert.Empty(executor.Actions);
            Assert.Equal(0, delayedTransport.GetPendingCallsCount());
        }

        [Fact]
        public void ForPendingCalls_UsingGrpcDelayedClientTransport_VerifyCallsInQueue()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);

            // Act
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            Assert.Equal(1, delayedTransport.GetPendingCallsCount());
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            Assert.Equal(2, delayedTransport.GetPendingCallsCount());
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);

            // Assert
            Assert.Empty(executor.Actions);
            Assert.Equal(3, delayedTransport.GetPendingCallsCount());
        }

        [Fact]
        public void ForPendingCallsAndReadyPicker_UsingGrpcDelayedClientTransportAndReprocess_VerifyCallsAreReprocessed()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);
            var picker = new SubchannelPickerFake((args) =>
            {
                var subChannel = new GrpcSubChannelFake(new Uri("http://10.0.0.60"), GrpcAttributes.Empty);
                return GrpcPickResult.WithSubChannel(subChannel);
            });

            // Act
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.Reprocess(picker);

            // Assert
            Assert.Single(executor.Actions);
            Assert.Equal(0, delayedTransport.GetPendingCallsCount());
        }

        [Fact]
        public void ForPendingCallsAndEmptyPicker_UsingGrpcDelayedClientTransportAndReprocess_VerifyCallsInQueue()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);
            var picker = new SubchannelPickerFake((args) => GrpcPickResult.WithNoResult());

            // Act
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.Reprocess(picker);

            // Assert
            Assert.Empty(executor.Actions);
            Assert.Equal(1, delayedTransport.GetPendingCallsCount());
        }

        [Fact]
        public void ForPendingCallsAndNullPicker_UsingGrpcDelayedClientTransportAndReprocess_VerifyCallsInQueue()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);

            // Act
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.Reprocess(null);

            // Assert
            Assert.Empty(executor.Actions);
            Assert.Equal(2, delayedTransport.GetPendingCallsCount());
        }

        [Fact]
        public void ForNoPendingCallsAndReadyPicker_UsingGrpcDelayedClientTransportAndReprocess_VerifyNoException()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);
            var picker = new SubchannelPickerFake((args) =>
            {
                var subChannel = new GrpcSubChannelFake(new Uri("http://10.0.0.60"), GrpcAttributes.Empty);
                return GrpcPickResult.WithSubChannel(subChannel);
            });

            // Act
            delayedTransport.Reprocess(picker);

            // Assert
            Assert.Empty(executor.Actions);
            Assert.Equal(0, delayedTransport.GetPendingCallsCount());
        }

        [Fact]
        public void ForPendingCallsAndErrorPicker_UsingGrpcDelayedClientTransportAndReprocess_VerifyThrowRpcException()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);
            var picker = new SubchannelPickerFake((args) =>
            {
                var errorStatus = new Status(StatusCode.Internal, "internal test bug");
                return GrpcPickResult.WithError(errorStatus);
            });
            Status? actualErrorStatus = null;

            // Act
            delayedTransport.BufforPendingCall((pickResult) => { actualErrorStatus = pickResult.Status; }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.Reprocess(picker);
            executor.DrainSingleAction();

            // Assert
            Assert.Empty(executor.Actions);
            Assert.Equal(0, delayedTransport.GetPendingCallsCount());
            Assert.Equal(StatusCode.Internal, actualErrorStatus?.StatusCode);
        }
        
        [Fact]
        public void ForShutdownInstance_UsingGrpcDelayedClientTransportBufforPendingCall_VerifyCallIsNotInQueue()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);
            Status? actualErrorStatus = null;

            // Act
            delayedTransport.ShutdownNow(new Status(StatusCode.Unavailable, "Dispose"));
            delayedTransport.BufforPendingCall((pickResult) => { actualErrorStatus = pickResult.Status; }, GrpcPickSubchannelArgs.Empty);
            executor.DrainSingleAction();

            // Assert
            Assert.Empty(executor.Actions);
            Assert.Equal(0, delayedTransport.GetPendingCallsCount());
            Assert.Equal(StatusCode.Unavailable, actualErrorStatus?.StatusCode);
        }

        [Fact]
        public void ForPendingCallsAndPreviousReadyPicker_UsingGrpcDelayedClientTransportAndReprocess_VerifyPreviousPickerIsUsed()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);
            var picker = new SubchannelPickerFake((args) =>
            {
                var subChannel = new GrpcSubChannelFake(new Uri("http://10.0.0.60"), GrpcAttributes.Empty);
                return GrpcPickResult.WithSubChannel(subChannel);
            });

            // Act
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.Reprocess(picker);
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);

            // Assert
            Assert.Equal(3, executor.Actions.Count);
            Assert.Equal(0, delayedTransport.GetPendingCallsCount());
        }

        [Fact]
        public void ForPendingCallsAndPreviousNotReadyPicker_UsingGrpcDelayedClientTransportAndReprocess_VerifyCallsInQueue()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);
            var picker = new SubchannelPickerFake((args) => GrpcPickResult.WithNoResult());

            // Act
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.Reprocess(picker);
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);

            // Assert
            Assert.Empty(executor.Actions);
            Assert.Equal(3, delayedTransport.GetPendingCallsCount());
        }

        [Fact]
        public void ForShutDown_UsingGrpcDelayedClientTransport_VerifyNoCallsInQueueAndStopProcessing()
        {
            // Arrange
            var executor = new ExecutorFake();
            var synchronizationContext = new GrpcSynchronizationContext((ex) => { });
            var delayedTransport = new GrpcDelayedClientTransport(executor, synchronizationContext);
            Status? actualErrorStatus = null;

            // Act
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            delayedTransport.BufforPendingCall((pickResult) => { }, GrpcPickSubchannelArgs.Empty);
            Assert.Equal(3, delayedTransport.GetPendingCallsCount());
            delayedTransport.ShutdownNow(new Status(StatusCode.Unavailable, "Dispose"));
            Assert.Empty(executor.Actions);
            Assert.Equal(0, delayedTransport.GetPendingCallsCount());
            delayedTransport.BufforPendingCall((pickResult) => { actualErrorStatus = pickResult.Status; }, GrpcPickSubchannelArgs.Empty);
            executor.DrainSingleAction();

            // Assert
            Assert.Empty(executor.Actions);
            Assert.Equal(0, delayedTransport.GetPendingCallsCount());
            Assert.Equal(StatusCode.Unavailable, actualErrorStatus?.StatusCode);
        }
    }
}
