using Grpc.Core;
using System;

//This class is not related to load balancing but rather to general gRPC channel implementation.
//In order to make is easier for future and moving file to better position in file tree it has namespace not related to loadbalancing.
namespace Grpc.Net.Client
{
    /// <summary>
    /// A tuple of a <see cref="GrpcConnectivityState"/> and its associated <see cref="Core.Status"/>.
    /// 
    /// If the state is <see cref="GrpcConnectivityState.TRANSIENT_FAILURE"/>, the status is never <see cref="StatusCode.OK"/>. For other
    /// states, the status is always <see cref="StatusCode.OK"/>.
    /// </summary>
    public sealed class GrpcConnectivityStateInfo
    {
        /// <summary>
        /// Returns the state.
        /// </summary>
        public GrpcConnectivityState State { get; }

        /// <summary>
        /// Returns the status associated with the state.
        /// 
        /// If the state is <see cref="GrpcConnectivityState.TRANSIENT_FAILURE"/>, the status is never <see cref="StatusCode.OK"/>. For other
        /// states, the status is always <see cref="StatusCode.OK"/>.
        /// </summary>
        public Status Status { get; }

        private GrpcConnectivityStateInfo(GrpcConnectivityState state, Status status)
        {
            State = state;
            Status = status;
        }

        /// <summary>
        /// Returns an instance for a state that is not <see cref="GrpcConnectivityState.TRANSIENT_FAILURE"/>.
        /// </summary>
        /// <param name="state">ConnectivityState value.</param>
        /// <returns>New instance of <see cref="GrpcConnectivityStateInfo"/>.</returns>
        public static GrpcConnectivityStateInfo ForNonError(GrpcConnectivityState state)
        {
            if (state == GrpcConnectivityState.TRANSIENT_FAILURE)
            {
                throw new ArgumentException("State is TRANSIENT_ERROR. Use ForTransientFailure() instead.");
            }
            return new GrpcConnectivityStateInfo(state, Status.DefaultSuccess);
        }

        /// <summary>
        /// Returns an instance for <see cref="GrpcConnectivityState.TRANSIENT_FAILURE"/>, associated with an error status.
        /// </summary>
        /// <param name="error">Non-OK status.</param>
        /// <returns>New instance of <see cref="GrpcConnectivityStateInfo"/>.</returns>
        public static GrpcConnectivityStateInfo ForTransientFailure(Status error)
        {
            if (error.StatusCode == StatusCode.OK)
            {
                throw new ArgumentException("The error status must not be OK.");
            }
            return new GrpcConnectivityStateInfo(GrpcConnectivityState.TRANSIENT_FAILURE, error);
        }
    }
}
