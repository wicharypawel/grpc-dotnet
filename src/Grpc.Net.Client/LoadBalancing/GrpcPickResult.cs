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
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// A balancing decision made by picker for an RPC.
    /// </summary>
    public sealed class GrpcPickResult
    {
        private static readonly GrpcPickResult NoResult = new GrpcPickResult(null, Status.DefaultSuccess, false);

        /// <summary>
        /// The Subchannel if this result was created by <see cref="WithSubChannel"/>, or null otherwise.
        /// </summary>
        public IGrpcSubChannel? SubChannel { get; }

        /// <summary>
        /// The status associated with this result. Non-OK if created using <see cref="WithError"/> method or <see cref="Status.DefaultSuccess"/> otherwise.
        /// </summary>                      
        public Status Status { get; }

        /// <summary>
        /// Returns true if this result was created by <see cref="WithDrop"/> method.
        /// </summary>
        public bool Drop { get; }

        private GrpcPickResult(IGrpcSubChannel? subChannel, Status status, bool drop)
        {
            SubChannel = subChannel;
            Status = status;
            Drop = drop;
        }

        /// <summary>
        /// A decision to proceed the RPC on a Subchannel.
        /// </summary>
        /// <param name="subChannel">Subchannel the picked Subchannel.</param>
        /// <returns>A balancing decision.</returns>
        public static GrpcPickResult WithSubChannel(IGrpcSubChannel subChannel)
        {
            if (subChannel == null)
            {
                throw new ArgumentNullException(nameof(subChannel));
            }
            return new GrpcPickResult(subChannel, Status.DefaultSuccess, false);
        }

        /// <summary>
        /// A decision to report a connectivity error to the RPC.
        /// </summary>
        /// <param name="error">Error the error status. Must not be OK.</param>
        /// <returns>A balancing decision.</returns>
        public static GrpcPickResult WithError(Status error)
        {
            if (error.StatusCode == StatusCode.OK)
            {
                throw new ArgumentException("Error status shouldn't be OK.");
            }
            return new GrpcPickResult(null, error, false);
        }

        /// <summary>
        /// A decision to fail an RPC immediately.  This is a final decision and will ignore retry policy.
        /// </summary>
        /// <param name="status">Status the status with which the RPC will fail. Must not be OK.</param>
        /// <returns>A balancing decision.</returns>
        public static GrpcPickResult WithDrop(Status status)
        {
            if (status.StatusCode == StatusCode.OK)
            {
                throw new ArgumentException("Drop status shouldn't be OK.");
            }
            return new GrpcPickResult(null, status, true);
        }

        /// <summary>
        /// No decision could be made.  The RPC will stay buffered.
        /// </summary>
        /// <returns>A balancing decision.</returns>
        public static GrpcPickResult WithNoResult()
        {
            return NoResult;
        }

        /// <summary>
        /// It determines whether the pick result implies a buffering.
        /// </summary>
        /// <param name="pickResult">The pick result to be checked.</param>
        /// <returns>True if the pick result is equal to the value returned by <see cref="WithNoResult"/>; otherwise, false.</returns>
        public static bool IsWithNoResult(GrpcPickResult pickResult)
        {
            return pickResult == NoResult;
        }
    }
}
