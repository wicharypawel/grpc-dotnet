﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grpc.Net.Client
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
    }

    /// <summary>
    /// The load balancing policy creates a subchannel to each server address.
    /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
    /// </summary>
    public interface IGrpcLoadBalancingPolicy : IDisposable
    {
        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        ILoggerFactory LoggerFactory { set; }

        /// <summary>
        /// Creates a subchannel to each server address. Depending on policy this may require additional 
        /// steps eg. reaching out to lookaside loadbalancer.
        /// </summary>
        /// <param name="resolutionResult">Resolved list of servers and/or lookaside load balancers.</param>
        /// <param name="serviceName">The name of the load balanced service (e.g., service.googleapis.com).</param>
        /// <param name="isSecureConnection">Flag if connection between client and destination server should be secured.</param>
        /// <returns>List of subchannels.</returns>
        Task CreateSubChannelsAsync(List<GrpcNameResolutionResult> resolutionResult, string serviceName, bool isSecureConnection);

        /// <summary>
        /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
        /// </summary>
        /// <returns>Selected subchannel.</returns>
        GrpcSubChannel GetNextSubChannel();
    }

    /// <summary>
    /// Resolved address of server or lookaside load balancer.
    /// </summary>
    public sealed class GrpcNameResolutionResult
    {
        /// <summary>
        /// Host address.
        /// </summary>
        public string Host { get; set; }
        
        /// <summary>
        /// Port.
        /// </summary>
        public int? Port { get; set; } = null;
        
        /// <summary>
        /// Flag that indicate if machine is load balancer or service.
        /// </summary>
        public bool IsLoadBalancer { get; set; } = false;

        /// <summary>
        /// Priority value, which was obtained from SRV record, for this Host. Default value zero.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Weight value, which was obtained from SRV record, for this Host. Default value zero.
        /// </summary>
        public int Weight { get; set; } = 0;

        /// <summary>
        /// Creates a <see cref="GrpcNameResolutionResult"/> with host and unassigned port.
        /// </summary>
        /// <param name="host">Host address of machine.</param>
        /// <param name="port">Machine port.</param>
        public GrpcNameResolutionResult(string host, int? port = null)
        {
            Host = host;
            Port = port;
        }
    }

    /// <summary>
    /// Address of server that can handle requests for RPC.
    /// </summary>
    public sealed class GrpcSubChannel
    {
        /// <summary>
        /// Gets the server address in Uri form.
        /// </summary>
        public Uri Address { get; }

        /// <summary>
        /// Gets LoadBalanceToken value. Token should be included into the initial metadata when client 
        /// starts a call to that server. The token is used by the server to report load to the gRPC LB system.
        /// If token is not present, string.Empty value is returned.
        /// </summary>
        public string LoadBalanceToken { get; }

        /// <summary>
        /// Creates a <see cref="GrpcSubChannel"/> object with subchannel address.
        /// </summary>
        /// <param name="address">SubChannel address</param>
        public GrpcSubChannel(Uri address) : this(address, string.Empty)
        {
        }

        /// <summary>
        /// Creates a <see cref="GrpcSubChannel"/> object with subchannel address.
        /// </summary>
        /// <param name="address">SubChannel address</param>
        /// <param name="loadBalanceToken">LoadBalanceToken for subchannel</param>
        public GrpcSubChannel(Uri address, string loadBalanceToken)
        {
            Address = address;
            LoadBalanceToken = loadBalanceToken ?? string.Empty;
        }
    }

    /// <summary>
    /// Assume name was already resolved or pass through to HttpClient to handle
    /// </summary>
    internal sealed class NoneResolverPlugin : IGrpcResolverPlugin
    {
        private ILogger _logger = NullLogger.Instance;
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<NoneResolverPlugin>();
        }

        public Task<List<GrpcNameResolutionResult>> StartNameResolutionAsync(Uri target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (target.Scheme.Equals("dns", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("dns:// scheme require non-default name resolver in channelOptions.ResolverPlugin");
            }
            _logger.LogDebug($"Name resolver using defined target as name resolution");
            return Task.FromResult(new List<GrpcNameResolutionResult>()
            {
               new GrpcNameResolutionResult(target.Host, target.Port)
            });
        }
    }

    internal sealed class PickFirstPolicy : IGrpcLoadBalancingPolicy
    {
        private ILogger _logger = NullLogger.Instance;
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<PickFirstPolicy>();
        }

        internal IReadOnlyList<GrpcSubChannel> SubChannels { get; set; } = Array.Empty<GrpcSubChannel>();

        public Task CreateSubChannelsAsync(List<GrpcNameResolutionResult> resolutionResult, string serviceName, bool isSecureConnection)
        {
            if (resolutionResult == null)
            {
                throw new ArgumentNullException(nameof(resolutionResult));
            }
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException($"{nameof(serviceName)} not defined");
            }
            resolutionResult = resolutionResult.Where(x => !x.IsLoadBalancer).ToList();
            if (resolutionResult.Count == 0)
            {
                throw new ArgumentException($"{nameof(resolutionResult)} must contain at least one non-blancer address");
            }
            _logger.LogDebug($"Start first_pick policy");
            var uriBuilder = new UriBuilder();
            uriBuilder.Host = resolutionResult[0].Host;
            uriBuilder.Port = resolutionResult[0].Port ?? (isSecureConnection ? 443 : 80);
            uriBuilder.Scheme = isSecureConnection ? "https" : "http";
            var uri = uriBuilder.Uri;
            var result = new List<GrpcSubChannel> {
                new GrpcSubChannel(uri)
            };
            _logger.LogDebug($"Found a server {uri}");
            _logger.LogDebug($"SubChannels list created");
            SubChannels = result;
            return Task.CompletedTask;
        }

        public GrpcSubChannel GetNextSubChannel()
        {
            return SubChannels[0];
        }

        public void Dispose()
        {
        }
    }
}
