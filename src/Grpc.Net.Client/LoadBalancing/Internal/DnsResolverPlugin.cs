using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port).
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// </summary>
    internal sealed class DnsResolverPlugin : IGrpcResolverPlugin
    {
        private ILogger _logger = NullLogger.Instance;
        private readonly string _defaultLoadBalancingPolicy;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<DnsResolverPlugin>();
        }

        /// <summary>
        /// Property created for testing purposes, allows setter injection
        /// </summary>
        internal Task<IPAddress[]>? OverrideDnsResults { private get; set; }

        /// <summary>
        /// Creates a new <seealso cref="DnsResolverPlugin"/> instance, with default settings.
        /// </summary>
        public DnsResolverPlugin() : this(GrpcAttributes.Empty)
        {
        }

        /// <summary>
        /// Creates a <seealso cref="DnsResolverPlugin"/> using specified <seealso cref="GrpcAttributes"/>.
        /// </summary>
        /// <param name="attributes">Attributes with options.</param>
        public DnsResolverPlugin(GrpcAttributes attributes)
        {
            _defaultLoadBalancingPolicy = attributes.Get(GrpcAttributesConstants.DefaultLoadBalancingPolicy) as string
                ?? "pick_first";
        }

        /// <summary>
        /// Name resolution for secified target.
        /// </summary>
        /// <param name="target">Server address with scheme.</param>
        /// <returns>List of resolved servers.</returns>
        public async Task<GrpcNameResolutionResult> StartNameResolutionAsync(Uri target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (!target.Scheme.Equals("dns", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{nameof(DnsResolverPlugin)} require dns:// scheme to set as target address");
            }
            var serversDnsQuery = target.Host;
            _logger.LogDebug($"Start A lookup for {serversDnsQuery}");
            var serversDnsQueryTask = OverrideDnsResults ?? Dns.GetHostAddressesAsync(serversDnsQuery);
            await serversDnsQueryTask.ConfigureAwait(false);
            var serversDnsQueryResults = serversDnsQueryTask.Result.Select(x => ParseARecord(x, target.Port, false)).ToArray();
            var results = serversDnsQueryResults.ToList();
            _logger.LogDebug($"NameResolution found {results.Count} DNS records");
            var serviceConfig = GrpcServiceConfig.Create(_defaultLoadBalancingPolicy);
            _logger.LogDebug($"Service config created with policies: {string.Join(',', serviceConfig.RequestedLoadBalancingPolicies)}");
            return new GrpcNameResolutionResult(results, GrpcServiceConfigOrError.FromConfig(serviceConfig), GrpcAttributes.Empty);
        }

        private GrpcHostAddress ParseARecord(IPAddress address, int port, bool isLoadBalancer)
        {
            _logger.LogDebug($"Found a A record {address.ToString()}");
            return new GrpcHostAddress(address.ToString())
            {
                Port = port,
                IsLoadBalancer = isLoadBalancer,
                Priority = 0,
                Weight = 0
            };
        }
    }
}
