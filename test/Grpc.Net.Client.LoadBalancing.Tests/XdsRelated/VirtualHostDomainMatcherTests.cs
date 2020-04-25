using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.XdsRelated
{
    /// <summary>
    /// This tests verify hostName matching against VirtualHost domains. 
    /// Source proto: envoy/api/v2/route/route_components.proto
    /// 
    /// Relevant documentation copied from source proto:
    /// A list of domains (host/authority header) that will be matched to this
    /// virtual host. Wildcard hosts are supported in the suffix or prefix form.
    ///
    /// Domain search order:
    ///  1. Exact domain names: ``www.foo.com``.
    ///  2. Suffix domain wildcards: ``*.foo.com`` or ``*-bar.foo.com``.
    ///  3. Prefix domain wildcards: ``foo.*`` or ``foo-*``.
    ///  4. Special wildcard ``*`` matching any domain.
    ///
    /// .. note::
    ///
    ///   The wildcard will not match the empty string.
    ///   e.g. ``*-bar.foo.com`` will match ``baz-bar.foo.com`` but not ``-bar.foo.com``.
    /// 
    /// Domains cannot contain control characters. This is validated by the well_known_regex HTTP_HEADER_VALUE.
    /// </summary>
    public sealed class VirtualHostDomainMatcherTests
    {
        [Fact]
        public void ForExactlyMatch_UseXdsResolverPluginMatchHostName_VerifyHostnames()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "foo.googleapis.com";
            Assert.False(XdsResolverPlugin.MatchHostName("bar.googleapis.com", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("fo.googleapis.com", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("oo.googleapis.com", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("googleapis.com", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("foo.googleapis", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("foo.googleapis.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("fOo.GOOGLEapis.com", pattern));
        }

        [Fact]
        public void ForPrefixWildcard_UseXdsResolverPluginMatchHostName_VerifyHostnames()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "*.foo.googleapis.com";
            Assert.False(XdsResolverPlugin.MatchHostName("foo.googleapis.com", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("bar-baz.foo.googleapis", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("bar.foo.googleapis.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("BAR.foo.googleAPIS.com", pattern));
            pattern = "*-bar.foo.googleapis.com";
            Assert.False(XdsResolverPlugin.MatchHostName("bar.foo.googleapis.com", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("baz-bar.foo.googleapis", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("-bar.foo.googleapis.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("baz-bar.foo.googleapis.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("BAZ-bar.foo.googleapis.com", pattern));
        }

        [Fact]
        public void ForSuffixfixWildcard_UseXdsResolverPluginMatchHostName_VerifyHostnames()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "foo.*";
            Assert.False(XdsResolverPlugin.MatchHostName("bar.googleapis.com", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("bar.foo.googleapis.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("foo.googleapis.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("foo.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("FOO.com", pattern));
            pattern = "foo-*";
            Assert.False(XdsResolverPlugin.MatchHostName("bar-.googleapis.com", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("foo.googleapis.com", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("foo.googleAPIS.COM", pattern));
            Assert.False(XdsResolverPlugin.MatchHostName("foo-", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("foo-bar.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("foo-.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("foo-bar", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("foo-bar.googleapis.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("foo-bar.GOOGLEapis.com", pattern));
        }

        [Fact]
        public void ForSpecialWildcard_UseXdsResolverPluginMatchHostName_VerifyHostnames()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "*";
            Assert.True(XdsResolverPlugin.MatchHostName("foo.googleapis.com", pattern));
            Assert.True(XdsResolverPlugin.MatchHostName("fOO.GOOgleapis.COm", pattern));
        }

        [Fact]
        public void ForWrongPatternsWithWildcard_UseXdsResolverPluginMatchHostName_VerifyReturnFalse()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "*.googleapis.*";
            Assert.False(XdsResolverPlugin.MatchHostName("foo.googleapis.com", pattern));
            pattern = "foo.*.com";
            Assert.False(XdsResolverPlugin.MatchHostName("foo.googleapis.com", pattern));
        }

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        [Fact]
        public void ForUnallowedHostNamesAndPatterns_UseXdsResolverPluginMatchHostName_VerifyThrowError()
        {
            // Arrange
            // Act
            // Assert
            Assert.Throws<ArgumentException>(() =>
            {
                XdsResolverPlugin.MatchHostName("*", null);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsResolverPlugin.MatchHostName("*", string.Empty);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsResolverPlugin.MatchHostName("*", "foo.googleapis.com.");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsResolverPlugin.MatchHostName("*", ".foo.googleapis.com");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsResolverPlugin.MatchHostName(null, "foo.googleapis.com");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsResolverPlugin.MatchHostName(string.Empty, "foo.googleapis.com");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsResolverPlugin.MatchHostName("foo.googleapis.com.", "foo.googleapis.com");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsResolverPlugin.MatchHostName(".foo.googleapis.com", "foo.googleapis.com");
            });
        }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
