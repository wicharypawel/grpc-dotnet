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
        public async Task ForRegisterCallbackBeforeStateChanged_UsingGrpcConnectivityStateManager_SuccessfullyCallBack()
        {
            // Arrange
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, GrpcConnectivityState.CONNECTING);
            Assert.Empty(results);
            stateManager.SetState(GrpcConnectivityState.TRANSIENT_FAILURE);
            await WaitUntilSizeChangeAsync(results, 0);
            
            // Assert
            Assert.Single(results);
            Assert.Contains(GrpcConnectivityState.TRANSIENT_FAILURE, results);
        }

        [Fact]
        public async Task ForRegisterCallbackAfterStateChanged_UsingGrpcConnectivityStateManager_SuccessfullyCallBack()
        {
            // Arrange
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, GrpcConnectivityState.IDLE);
            await WaitUntilSizeChangeAsync(results, 0);

            // Assert
            Assert.Single(results);
            Assert.Contains(GrpcConnectivityState.CONNECTING, results);
        }

        [Fact]
        public async Task ForCallbackOnlyCalledOnTransition_UsingGrpcConnectivityStateManager_NoCallback()
        {
            // Arrange
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, GrpcConnectivityState.IDLE);
            stateManager.SetState(GrpcConnectivityState.IDLE);
            await WaitUntilSizeChangeAsync(results, 0, TimeSpan.FromMilliseconds(100));

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task ForCallbacksAreOneShot_UsingGrpcConnectivityStateManager_NoRepetingOnCallback()
        {
            // Arrange
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, GrpcConnectivityState.IDLE);
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            await WaitUntilSizeChangeAsync(results, 0);
            stateManager.SetState(GrpcConnectivityState.READY);
            await WaitUntilSizeChangeAsync(results, 1, TimeSpan.FromMilliseconds(100));

            // Assert
            Assert.Single(results);
            Assert.Contains(GrpcConnectivityState.CONNECTING, results);
        }

        [Fact]
        public async Task ForMultipleCallbacks_UsingGrpcConnectivityStateManager_SuccessfullyCallBack()
        {
            // Arrange
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, GrpcConnectivityState.IDLE);
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, GrpcConnectivityState.IDLE);
            stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, GrpcConnectivityState.READY);
            await WaitUntilSizeChangeAsync(results, 0); //last callback is run immediately because the source state is already different from the current
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            await WaitUntilSizeChangeAsync(results, 1);
            await WaitUntilSizeChangeAsync(results, 2);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Contains(GrpcConnectivityState.IDLE, results);
            Assert.Contains(GrpcConnectivityState.CONNECTING, results);
            Assert.Contains(GrpcConnectivityState.CONNECTING, results);
        }

        [Fact]
        public async Task ForRegisterCallbackFromCallback_UsingGrpcConnectivityStateManager_SuccessfullyRegisterCallBack()
        {
            // Arrange
            var stateManager = new GrpcConnectivityStateManager();
            var results = new List<GrpcConnectivityState>();

            // Act
            stateManager.NotifyWhenStateChanged(() => 
            { 
                results.Add(stateManager.GetState());
                stateManager.NotifyWhenStateChanged(() => { results.Add(stateManager.GetState()); }, stateManager.GetState());
            }, GrpcConnectivityState.IDLE);
            stateManager.SetState(GrpcConnectivityState.CONNECTING);
            await WaitUntilSizeChangeAsync(results, 0);
            stateManager.SetState(GrpcConnectivityState.READY);
            await WaitUntilSizeChangeAsync(results, 1);

            // Assert
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

        private static async Task WaitUntilSizeChangeAsync(List<GrpcConnectivityState> list, int initialSize, TimeSpan? timeout = null)
        {
            var timeoutTask = timeout != null ? Task.Delay(timeout.Value) : null;
            while (list.Count == initialSize && !(timeoutTask?.IsCompleted ?? false))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }
        }
    }
}
