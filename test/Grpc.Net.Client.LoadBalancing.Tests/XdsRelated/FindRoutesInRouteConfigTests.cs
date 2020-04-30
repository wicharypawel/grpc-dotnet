using Envoy.Api.V2.Route;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Factories;
using System.Collections.Generic;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.XdsRelated
{
    public sealed class FindRoutesInRouteConfigTests
    {
        [Fact]
        public void ForExactMatchFirst_UseFindRoutesInRouteConfig_ReturnRouteWithClusterName()
        {
            // Arrange
            var hostname = "a.googleapis.com";
            var targetClusterName = "cluster-hello.googleapis.com";
            var vHost1 = new VirtualHost() { Name = "virtualhost01.googleapis.com" /*don't care*/ };
            vHost1.Domains.AddRange(new string[] { "a.googleapis.com", "b.googleapis.com" });
            vHost1.Routes.Add(new Route()
            {
                Route_ = new RouteAction() { Cluster = targetClusterName },
                Match = new RouteMatch() { Prefix = "" }
            });
            var vHost2 = new VirtualHost() { Name = "virtualhost02.googleapis.com" /*don't care*/ };
            vHost2.Domains.AddRange(new string[] { "*.googleapis.com" });
            vHost2.Routes.Add(new Route()
            {
                Route_ = new RouteAction() { Cluster = "cluster-hi.googleapis.com" },
                Match = new RouteMatch() { Prefix = "" }
            });
            var vHost3 = new VirtualHost() { Name = "virtualhost03.googleapis.com" /*don't care*/ };
            vHost3.Domains.AddRange(new string[] { "*" });
            vHost3.Routes.Add(new Route()
            {
                Route_ = new RouteAction() { Cluster = "cluster-hey.googleapis.com" },
                Match = new RouteMatch() { Prefix = "" }
            });
            var routeConfig = XdsClientTestFactory.BuildRouteConfiguration("route-foo.googleapis.com", new List<VirtualHost>() { vHost1, vHost2, vHost3 });

            // Act
            var routes = XdsClient.FindRoutesInRouteConfig(routeConfig, hostname);

            // Assert
            Assert.Single(routes);
            Assert.Equal(targetClusterName, routes[0].Route_.Cluster);
        }

        [Fact]
        public void ForPreferSuffixDomainOverPrefixDomain_UseFindRoutesInRouteConfig_ReturnRouteWithClusterName()
        {
            // Arrange
            var hostname = "a.googleapis.com";
            var targetClusterName = "cluster-hello.googleapis.com";
            var vHost1 = new VirtualHost() { Name = "virtualhost01.googleapis.com" /*don't care*/ };
            vHost1.Domains.AddRange(new string[] { "*.googleapis.com", "b.googleapis.com" });
            vHost1.Routes.Add(new Route()
            {
                Route_ = new RouteAction() { Cluster = targetClusterName },
                Match = new RouteMatch() { Prefix = "" }
            });
            var vHost2 = new VirtualHost() { Name = "virtualhost02.googleapis.com" /*don't care*/ };
            vHost2.Domains.AddRange(new string[] { "a.googleapis.*" });
            vHost2.Routes.Add(new Route()
            {
                Route_ = new RouteAction() { Cluster = "cluster-hi.googleapis.com" },
                Match = new RouteMatch() { Prefix = "" }
            });
            var vHost3 = new VirtualHost() { Name = "virtualhost03.googleapis.com" /*don't care*/ };
            vHost3.Domains.AddRange(new string[] { "*" });
            vHost3.Routes.Add(new Route()
            {
                Route_ = new RouteAction() { Cluster = "cluster-hey.googleapis.com" },
                Match = new RouteMatch() { Prefix = "" }
            });
            var routeConfig = XdsClientTestFactory.BuildRouteConfiguration("route-foo.googleapis.com", new List<VirtualHost>() { vHost1, vHost2, vHost3 });
            
            // Act
            var routes = XdsClient.FindRoutesInRouteConfig(routeConfig, hostname);

            // Assert
            Assert.Single(routes);
            Assert.Equal(targetClusterName, routes[0].Route_.Cluster);
        }

        [Fact]
        public void ForAsteriskMatchAnyDomain_UseFindRoutesInRouteConfig_ReturnRouteWithClusterName()
        {
            // Arrange
            var hostname = "a.googleapis.com";
            var targetClusterName = "cluster-hello.googleapis.com";
            var vHost1 = new VirtualHost() { Name = "virtualhost01.googleapis.com" /*don't care*/ };
            vHost1.Domains.AddRange(new string[] { "*" });
            vHost1.Routes.Add(new Route()
            {
                Route_ = new RouteAction() { Cluster = targetClusterName },
                Match = new RouteMatch() { Prefix = "" }
            });
            var vHost2 = new VirtualHost() { Name = "virtualhost02.googleapis.com" /*don't care*/ };
            vHost2.Domains.AddRange(new string[] { "b.googleapis.com" });
            vHost2.Routes.Add(new Route()
            {
                Route_ = new RouteAction() { Cluster = "cluster-hi.googleapis.com" },
                Match = new RouteMatch() { Prefix = "" }
            });
            var routeConfig = XdsClientTestFactory.BuildRouteConfiguration("route-foo.googleapis.com", new List<VirtualHost>() { vHost1, vHost2 });

            // Act
            var routes = XdsClient.FindRoutesInRouteConfig(routeConfig, hostname);

            // Assert
            Assert.Single(routes);
            Assert.Equal(targetClusterName, routes[0].Route_.Cluster);
        }
    }
}
