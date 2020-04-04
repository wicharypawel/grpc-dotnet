using DnsClient;
using DnsClient.Protocol;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing.Extensions
{
    /// <summary>
    /// Resolver plugin is responsible for name resolution by reaching the authority and return 
    /// a list of resolved addresses (both IP address and port).
    /// More: https://github.com/grpc/grpc/blob/master/doc/naming.md
    /// </summary>
    public sealed class DnsClientResolverPlugin : IGrpcResolverPlugin
    {
        private DnsClientResolverPluginOptions _options;
        private ILogger _logger = NullLogger.Instance;
        private GrpcServiceConfig? _serviceConfig;

        /// <summary>
        /// LoggerFactory is configured (injected) when class is being instantiated.
        /// </summary>
        public ILoggerFactory LoggerFactory
        {
            set => _logger = value.CreateLogger<DnsClientResolverPlugin>();
        }

        /// <summary>
        /// Property created for testing purposes, allows setter injection
        /// </summary>
        internal IDnsQuery? OverrideDnsClient { private get; set; }

        /// <summary>
        /// Creates a <seealso cref="DnsClientResolverPlugin"/> using default <seealso cref="DnsClientResolverPluginOptions"/>.
        /// </summary>
        public DnsClientResolverPlugin() : this(new DnsClientResolverPluginOptions())
        {
        }

        /// <summary>
        /// Creates a <seealso cref="DnsClientResolverPlugin"/> using specified <seealso cref="DnsClientResolverPluginOptions"/>.
        /// </summary>
        /// <param name="options">Options allows override default behaviour.</param>
        public DnsClientResolverPlugin(DnsClientResolverPluginOptions options)
        {
            _options = options;
        }

        /// <summary>
        /// Name resolution for secified target.
        /// </summary>
        /// <param name="target">Server address with scheme.</param>
        /// <returns>List of resolved servers and/or lookaside load balancers.</returns>
        public async Task<List<GrpcNameResolutionResult>> StartNameResolutionAsync(Uri target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (!target.Scheme.Equals("dns", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{nameof(DnsClientResolverPlugin)} require dns:// scheme to set as target address");
            }
            var host = target.Host;
            var dnsClient = GetDnsClient();
            if (_options.EnableTxtServiceConfig)
            {
                var serviceConfigDnsQuery = $"_grpc_config.{host}";
                _logger.LogDebug($"Start TXT lookup for {serviceConfigDnsQuery}");
                var txtRecords = (await dnsClient.QueryAsync(serviceConfigDnsQuery, QueryType.TXT).ConfigureAwait(false)).Answers.OfType<TxtRecord>().ToArray();
                _logger.LogDebug($"Number of TXT records found: {txtRecords.Length}");
                var grpcConfigs = txtRecords.SelectMany(x => x.Text).Where(IsGrpcConfigTxtRecord).ToArray();
                _logger.LogDebug($"Number of grpc_configs found: {grpcConfigs.Length}");
                if(grpcConfigs.Length != 0 && TryParseGrpcConfig(grpcConfigs[0], out var serviceConfigs))
                {
                    _logger.LogDebug($"First grpc_config selected ");
                    _logger.LogDebug($"Parsing JSON grpc_config into service config success");
                    var serviceConfig = serviceConfigs[0];
                    var loadBalancingPolicies = serviceConfig.GetLoadBalancingPolicies();                  
                    _serviceConfig = new GrpcServiceConfig
                    {
                        RequestedLoadBalancingPolicies = loadBalancingPolicies
                    };
                }
                else
                {
                    _logger.LogDebug($"Parsing JSON grpc_config into service config failed, loading service config is skipped");
                }
            }
            var balancingDnsQueryResults = Array.Empty<GrpcNameResolutionResult>();
            if (_options.EnableSrvGrpclb)
            {
                var balancingDnsQuery = $"_grpclb._tcp.{host}";
                _logger.LogDebug($"Start SRV lookup for {balancingDnsQuery}");
                var balancingDnsQueryTask = dnsClient.QueryAsync(balancingDnsQuery, QueryType.SRV);
                await balancingDnsQueryTask.ConfigureAwait(false);
                balancingDnsQueryResults = balancingDnsQueryTask.Result.Answers.OfType<SrvRecord>().Select(x => ParseSrvRecord(x, true)).ToArray();
                if(_serviceConfig == null && balancingDnsQueryResults.Length > 0)
                {
                    _serviceConfig = GrpcServiceConfig.Create("grpclb", "pick_first");
                }
            }
            var serversDnsQuery = host;
            _logger.LogDebug($"Start A lookup for {serversDnsQuery}");
            var serversDnsQueryTask = dnsClient.QueryAsync(serversDnsQuery, QueryType.A);
            await serversDnsQueryTask.ConfigureAwait(false);
            var serversDnsQueryResults = serversDnsQueryTask.Result.Answers.OfType<ARecord>().Select(x => ParseARecord(x, target.Port, false)).ToArray();
            var results = balancingDnsQueryResults.Union(serversDnsQueryResults).ToList();
            if (_serviceConfig == null)
            {
                _serviceConfig = GrpcServiceConfig.Create("pick_first");
            }
            _logger.LogDebug($"NameResolution found {results.Count} DNS records");
            return results;
        }

        private IDnsQuery GetDnsClient()
        {
            if (OverrideDnsClient != null)
            {
                return OverrideDnsClient;
            }
            if (_options.NameServers.Length == 0)
            {
                return new LookupClient();
            }
            else
            {
                _logger.LogDebug($"Override DNS name servers using options");
                return new LookupClient(_options.NameServers);
            }
        }

        private static bool IsGrpcConfigTxtRecord(string txtRecordText)
        {
            return txtRecordText.StartsWith("grpc_config=", StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool TryParseGrpcConfig(string txtRecordText, out ServiceConfigModel[] serviceConfigs)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var txtRecordValue = txtRecordText.Substring(12); // remove txt key -> grpc_config= 
                serviceConfigs = JsonSerializer.Deserialize<GrpcConfigModel[]>(txtRecordValue, options)
                    .Select(x => x.ServiceConfig).ToArray();
                return true;
            }
            catch (Exception)
            {
                serviceConfigs = Array.Empty<ServiceConfigModel>();
                return false;
            }
        }
        
        private GrpcNameResolutionResult ParseSrvRecord(SrvRecord srvRecord, bool isLoadBalancer)
        {
            _logger.LogDebug($"Found a SRV record {srvRecord.ToString()}");
            return new GrpcNameResolutionResult(srvRecord.Target)
            {
                Port = srvRecord.Port,
                IsLoadBalancer = isLoadBalancer,
                Priority = srvRecord.Priority,
                Weight = srvRecord.Weight
            };
        }

        private GrpcNameResolutionResult ParseARecord(ARecord aRecord, int port, bool isLoadBalancer)
        {
            _logger.LogDebug($"Found a A record {aRecord.ToString()}");
            return new GrpcNameResolutionResult(aRecord.Address.ToString())
            {
                Port = port,
                IsLoadBalancer = isLoadBalancer,
                Priority = 0,
                Weight = 0
            };
        }

        /// <summary>
        /// Returns load balancing configuration discovered during name resolution.
        /// </summary>
        /// <returns>Load balancing configuration.</returns>
        public Task<GrpcServiceConfig> GetServiceConfigAsync()
        {
            if (_serviceConfig == null)
            {
                throw new InvalidOperationException("Can not get service config before name resolution");
            }
            _logger.LogDebug($"Service config created with policies: {string.Join(',', _serviceConfig.RequestedLoadBalancingPolicies)}");
            return Task.FromResult(_serviceConfig);
        }
    }
}
