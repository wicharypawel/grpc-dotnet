using Grpc.Core;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Wraps parsed service config or an parsing error details.
    /// </summary>
    public sealed class GrpcServiceConfigOrError
    {
        /// <summary>
        /// Returns config if exists, otherwise null.
        /// </summary>
        public object? Config { get; }

        /// <summary>
        /// Returns error status if exists, otherwise null.
        /// </summary>
        public Status? Status { get; }

        private GrpcServiceConfigOrError(object? config, Status? status)
        {
            Config = config;
            Status = status;
        }

        /// <summary>
        /// Returns a <see cref="GrpcServiceConfigOrError"/> for the successfully parsed config.
        /// </summary>
        /// <param name="config">Parsed config.</param>
        /// <returns>Instance of <see cref="GrpcServiceConfigOrError"/>.</returns>
        public static GrpcServiceConfigOrError FromConfig(object config)
        {
            if (config == null)
            {
                throw new System.ArgumentNullException(nameof(config));
            }
            return new GrpcServiceConfigOrError(config, null);
        }

        /// <summary>
        /// Returns a <see cref="GrpcServiceConfigOrError"/> for the failure to parse the config.
        /// </summary>
        /// <param name="status">Parsing status error.</param>
        /// <returns>Instance of <see cref="GrpcServiceConfigOrError"/>.</returns>
        public static GrpcServiceConfigOrError FromError(Status status)
        {
            if (status.StatusCode == StatusCode.OK)
            {
                throw new System.ArgumentException($"Can not use OK {nameof(status)}.");
            }
            return new GrpcServiceConfigOrError(null, status);
        }
    }
}
