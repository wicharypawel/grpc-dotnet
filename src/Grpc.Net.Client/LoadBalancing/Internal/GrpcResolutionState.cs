namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// A <see cref="GrpcResolutionState"/> indicates the status of last name resolution.
    /// </summary>
    internal enum GrpcResolutionState
    {
        NoResolution,
        Success,
        Error
    }
}
