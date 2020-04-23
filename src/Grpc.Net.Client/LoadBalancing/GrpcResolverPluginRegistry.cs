using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Registry of <seealso cref="IGrpcResolverPluginProvider"/>s. 
    /// </summary>
    public sealed class GrpcResolverPluginRegistry
    {
        private readonly List<IGrpcResolverPluginProvider> _providers = new List<IGrpcResolverPluginProvider>();
        private IReadOnlyList<IGrpcResolverPluginProvider> _effectiveProviders = Array.Empty<IGrpcResolverPluginProvider>();

        private GrpcResolverPluginRegistry()
        {
        }

        private static GrpcResolverPluginRegistry? Instance;
        private static readonly object LockObject = new object();

        /// <summary>
        /// Register provider.
        /// </summary>
        public void Register(IGrpcResolverPluginProvider provider)
        {
            if (!provider.IsAvailable)
            {
                return;
            }
            lock (LockObject)
            {
                _providers.Add(provider);
                _providers.Sort(new ProvidersComparer());
                _effectiveProviders = _providers.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Deregisters provider.
        /// </summary>
        public void Deregister(IGrpcResolverPluginProvider provider)
        {
            lock (LockObject)
            {
                _providers.Remove(provider);
                _providers.Sort(new ProvidersComparer());
                _effectiveProviders = _providers.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Returns the provider for the given scheme. 
        /// </summary>
        /// <param name="scheme">Scheme used for target written eg. http, dns, xds etc.</param>
        /// <returns>ResolverPluginProvider or null if no suitable provider can be found.</returns>
        public IGrpcResolverPluginProvider? GetProvider(string scheme)
        {
            foreach (var provider in _effectiveProviders)
            {
                if(scheme.Equals(provider.Scheme, StringComparison.OrdinalIgnoreCase))
                {
                    return provider;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the default registry.
        /// </summary>
        /// <returns>Instance of <seealso cref="GrpcResolverPluginRegistry"/>.</returns>
        public static GrpcResolverPluginRegistry GetDefaultRegistry()
        {
            return GetDefaultRegistry(NullLoggerFactory.Instance);
        }

        /// <summary>
        /// Returns the default registry.
        /// </summary>
        /// <param name="loggerFactory">Logger factory instance.</param>
        /// <returns>Instance of <seealso cref="GrpcResolverPluginRegistry"/>.</returns>
        public static GrpcResolverPluginRegistry GetDefaultRegistry(ILoggerFactory loggerFactory)
        {
            if (Instance != null)
            {
                return Instance;
            }
            lock (LockObject)
            {
                if (Instance == null)
                {
                    var logger = loggerFactory.CreateLogger<GrpcResolverPluginRegistry>();
                    Instance = new GrpcResolverPluginRegistry();
                    logger.LogDebug($"{nameof(GrpcResolverPluginRegistry)} created");
                    foreach (var provider in GetResolverProvidersFromAppDomain())
                    {
                        Instance.Register(provider);
                        logger.LogDebug($"{nameof(GrpcResolverPluginRegistry)} found {provider.GetType().Name} for scheme {provider.Scheme}");
                    }
                }
                return Instance;
            }
        }

        /// <summary>
        /// Creates an empty registry, used for tests only.
        /// </summary>
        /// <returns>Instance of <seealso cref="GrpcResolverPluginRegistry"/>.</returns>
        internal static GrpcResolverPluginRegistry CreateEmptyRegistry()
        {
            return new GrpcResolverPluginRegistry();
        }

        private static IGrpcResolverPluginProvider[] GetResolverProvidersFromAppDomain()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Where(assembly => assembly.FullName.StartsWith("Grpc.Net.Client", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return assemblies.SelectMany(GetResolverProvidersFromAssembly).ToArray();
        }

        private static IGrpcResolverPluginProvider[] GetResolverProvidersFromAssembly(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract)
                .Where(type => typeof(IGrpcResolverPluginProvider).IsAssignableFrom(type))
                .Select(type => Activator.CreateInstance(type))
                .Cast<IGrpcResolverPluginProvider>()
                .ToArray();
        }

        private sealed class ProvidersComparer : IComparer<IGrpcResolverPluginProvider>
        {
            public int Compare(IGrpcResolverPluginProvider x, IGrpcResolverPluginProvider y)
            {
                return y.Priority.CompareTo(x.Priority);
            }
        }
    }
}
