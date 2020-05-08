using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

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
        private Uri? _target = null;
        private IGrpcNameResolutionObserver? _observer = null;
        private CancellationTokenSource? _cancellationTokenSource = null;

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

        public void Subscribe(Uri target, IGrpcNameResolutionObserver observer)
        {
            if (_observer != null)
            {
                throw new InvalidOperationException("Observer already registered.");
            }
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _cancellationTokenSource = new CancellationTokenSource();
            Resolve();
        }

        public void Unsubscribe()
        {
            _observer = null;
            _target = null;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        public void RefreshResolution()
        {
            if (_observer == null)
            {
                throw new InvalidOperationException("Observer not registered.");
            }
            Resolve();
        }

        private void Resolve()
        {
            Task.Factory.StartNew(async () => await ResolveCoreAsync(_target, _observer).ConfigureAwait(false), _cancellationTokenSource!.Token);
        }

        private async Task ResolveCoreAsync(Uri? target, IGrpcNameResolutionObserver? observer)
        {
            if (observer == null)
            {
                return;
            }
            if (target == null)
            {
                observer.OnError(new Core.Status(Core.StatusCode.Unavailable, "Target is empty."));
                return;
            }
            if (!target.Scheme.Equals("dns", StringComparison.OrdinalIgnoreCase))
            {
                observer.OnError(new Core.Status(Core.StatusCode.Unavailable, $"{nameof(DnsResolverPlugin)} require dns:// scheme to set as target address."));
                return;
            }
            var serversDnsQuery = target.Host;
            _logger.LogDebug($"Start A lookup for {serversDnsQuery}");
            try
            {
                var serversDnsQueryTask = OverrideDnsResults ?? Dns.GetHostAddressesAsync(serversDnsQuery);
                await serversDnsQueryTask.ConfigureAwait(false);
                var serversDnsQueryResults = serversDnsQueryTask.Result.Select(x => ParseARecord(x, target.Port, false)).ToArray();
                var results = serversDnsQueryResults.ToList();
                _logger.LogDebug($"NameResolution found {results.Count} DNS records");
                var serviceConfig = GrpcServiceConfig.Create(_defaultLoadBalancingPolicy);
                _logger.LogDebug($"Service config created with policies: {string.Join(',', serviceConfig.RequestedLoadBalancingPolicies)}");
                observer.OnNext(new GrpcNameResolutionResult(results, GrpcServiceConfigOrError.FromConfig(serviceConfig), GrpcAttributes.Empty));
            }
            catch (Exception ex)
            {
                observer.OnError(new Core.Status(Core.StatusCode.Unavailable, ex.Message));
            }
        }

        public void Dispose()
        {
            Unsubscribe();
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
