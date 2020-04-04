﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port) and a service config.
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// </summary>
    public interface IGrpcResolverPlugin
    {
        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        ILoggerFactory LoggerFactory { set; }

        /// <summary>
        /// Name resolution for secified target.
        /// </summary>
        /// <param name="target">Server address with scheme.</param>
        /// <returns>List of resolved servers and/or lookaside load balancers.</returns>
        Task<List<GrpcNameResolutionResult>> StartNameResolutionAsync(Uri target);

        /// <summary>
        /// Returns load balancing configuration discovered during name resolution.
        /// </summary>
        /// <returns>Load balancing configuration.</returns>
        Task<GrpcServiceConfig> GetServiceConfigAsync();
    }
}
