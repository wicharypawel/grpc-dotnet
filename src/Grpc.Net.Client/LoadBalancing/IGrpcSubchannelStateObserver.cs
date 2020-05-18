namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Receives state changes for one <see cref="IGrpcSubChannel"/>. All methods are run under 
    /// <see cref="IGrpcHelper.GetSynchronizationContext"/>.
    /// </summary>
    public interface IGrpcSubchannelStateObserver
    {
        /// <summary>
        /// Handles a state change on a Subchannel.
        /// 
        /// The initial state of a Subchannel is IDLE. You won't get a notification for the initial
        /// IDLE state.
        /// 
        /// If the new state is not SHUTDOWN, this method should create a new picker and call
        /// <see cref="GrpcHelper.UpdateBalancingState"/>. Failing to do so may result in
        /// unnecessary delays of RPCs. Please refer to <see cref="GrpcPickResult.WithSubChannel"/>
        /// for more information.
        /// 
        /// LoadBalancer usually don't need to react to a SHUTDOWN state.
        /// </summary>
        /// <param name="value">NewState the new state.</param>
        public void OnNext(GrpcConnectivityStateInfo value);
    }
}
