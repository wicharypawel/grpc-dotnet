using Grpc.Core;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// An interface for objects that react to changes of <seealso cref="GrpcNameResolutionResult"/>.
    /// Receives address updates. All methods are expected to return quickly.
    /// </summary>
    public interface IGrpcNameResolutionObserver
    {
        /// <summary>
        /// Handles updates on resolved addresses and attributes.
        /// </summary>
        /// <param name="value">ResolutionResult the resolved server addresses, attributes, and Service Config.</param>
        public void OnNext(GrpcNameResolutionResult value);

        /// <summary>
        /// Handles a name resolving error from the resolver. The observer is responsible for eventually
        /// invoking Refresh method to re-attempt resolution.
        /// </summary>
        /// <param name="error">Error a non-OK status.</param>
        public void OnError(Status error);
    }
}
