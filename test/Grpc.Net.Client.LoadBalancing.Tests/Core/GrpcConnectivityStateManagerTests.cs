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

using Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcConnectivityStateManagerTests
    {
        [Fact]
        public void ForNoCallback_UsingGrpcConnectivityStateManager_SuccessfullySwitchingStates()
        {
            // Arrange
            var stateManager = new GrpcConnectivityStateManager();

            // Act
            Assert.Equal(GrpcConnectivityState.IDLE, stateManager.GetState()); // assert initial state
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            Assert.Equal(GrpcConnectivityState.CONNECTING, stateManager.GetState());
            stateManager.SetState(GrpcConnectivityState.TRANSIENT_FAILURE);

            // Assert
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, stateManager.GetState());
        }

        [Fact]
        public void ForRegisterCallbackBeforeStateChanged_UsingGrpcConnectivityStateManager_SuccessfullyCallBack()
        {
            // Arrange
            var executor = new ExecutorFake();
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, executor, GrpcConnectivityState.CONNECTING);
            Assert.Empty(results);
            stateManager.SetState(GrpcConnectivityState.TRANSIENT_FAILURE);
            executor.DrainSingleAction();
            
            // Assert
            Assert.Single(results);
            Assert.Contains(GrpcConnectivityState.TRANSIENT_FAILURE, results);
        }

        [Fact]
        public void ForRegisterCallbackAfterStateChanged_UsingGrpcConnectivityStateManager_SuccessfullyCallBack()
        {
            // Arrange
            var executor = new ExecutorFake();
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, executor, GrpcConnectivityState.IDLE);
            executor.DrainSingleAction();

            // Assert
            Assert.Single(results);
            Assert.Contains(GrpcConnectivityState.CONNECTING, results);
        }

        [Fact]
        public void ForCallbackOnlyCalledOnTransition_UsingGrpcConnectivityStateManager_NoCallback()
        {
            // Arrange
            var executor = new ExecutorFake();
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, executor, GrpcConnectivityState.IDLE);
            stateManager.SetState(GrpcConnectivityState.IDLE);

            // Assert
            Assert.True(executor.Actions.Count == 0);
            Assert.Empty(results);
        }

        [Fact]
        public void ForCallbacksAreOneShot_UsingGrpcConnectivityStateManager_NoRepetingOnCallback()
        {
            // Arrange
            var executor = new ExecutorFake();
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, executor, GrpcConnectivityState.IDLE);
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            executor.DrainSingleAction();
            stateManager.SetState(GrpcConnectivityState.READY);

            // Assert
            Assert.True(executor.Actions.Count == 0);
            Assert.Single(results);
            Assert.Contains(GrpcConnectivityState.CONNECTING, results);
        }

        [Fact]
        public void ForMultipleCallbacks_UsingGrpcConnectivityStateManager_SuccessfullyCallBack()
        {
            // Arrange
            var executor = new ExecutorFake();
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, executor, GrpcConnectivityState.IDLE);
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, executor, GrpcConnectivityState.IDLE);
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, executor, GrpcConnectivityState.READY);
            executor.DrainSingleAction(); //last callback is run immediately because the source state is already different from the current
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            executor.DrainSingleAction();
            executor.DrainSingleAction();

            // Assert
            Assert.True(executor.Actions.Count == 0);
            Assert.Equal(3, results.Count);
            Assert.Contains(GrpcConnectivityState.IDLE, results);
            Assert.Contains(GrpcConnectivityState.CONNECTING, results);
            Assert.Contains(GrpcConnectivityState.CONNECTING, results);
        }

        [Fact]
        public void ForRegisterCallbackFromCallback_UsingGrpcConnectivityStateManager_SuccessfullyRegisterCallBack()
        {
            // Arrange
            var executor = new ExecutorFake();
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.NotifyWhenStateChanged(() => 
            { 
                results.Add(stateManager.GetState());
                stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, executor, stateManager.GetState());
            }, executor, GrpcConnectivityState.IDLE);
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            executor.DrainSingleAction();
            stateManager.SetState(GrpcConnectivityState.READY);
            executor.DrainSingleAction();

            // Assert
            Assert.True(executor.Actions.Count == 0);
            Assert.Equal(2, results.Count);
            Assert.Contains(GrpcConnectivityState.CONNECTING, results);
            Assert.Contains(GrpcConnectivityState.READY, results);
        }

        [Fact]
        public void ForShutdownThenReady_UsingGrpcConnectivityStateManager_DoNotChangeStateWhenShutdown()
        {
            // Arrange
            var stateManager = new GrpcConnectivityStateManager();

            // Act
            stateManager.SetState(GrpcConnectivityState.SHUTDOWN);
            Assert.Equal(GrpcConnectivityState.SHUTDOWN, stateManager.GetState());
            stateManager.SetState(GrpcConnectivityState.READY);

            // Assert
            Assert.Equal(GrpcConnectivityState.SHUTDOWN, stateManager.GetState());
        }
    }
}
