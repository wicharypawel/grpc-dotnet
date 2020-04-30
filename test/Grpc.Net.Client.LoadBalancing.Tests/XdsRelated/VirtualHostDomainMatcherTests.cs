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
        public void ForExactlyMatch_UseXdsClientMatchHostName_VerifyHostnames()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "foo.googleapis.com";
            Assert.False(XdsClient.MatchHostName("bar.googleapis.com", pattern));
            Assert.False(XdsClient.MatchHostName("fo.googleapis.com", pattern));
            Assert.False(XdsClient.MatchHostName("oo.googleapis.com", pattern));
            Assert.False(XdsClient.MatchHostName("googleapis.com", pattern));
            Assert.False(XdsClient.MatchHostName("foo.googleapis", pattern));
            Assert.True(XdsClient.MatchHostName("foo.googleapis.com", pattern));
            Assert.True(XdsClient.MatchHostName("fOo.GOOGLEapis.com", pattern));
        }

        [Fact]
        public void ForPrefixWildcard_UseXdsClientMatchHostName_VerifyHostnames()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "*.foo.googleapis.com";
            Assert.False(XdsClient.MatchHostName("foo.googleapis.com", pattern));
            Assert.False(XdsClient.MatchHostName("bar-baz.foo.googleapis", pattern));
            Assert.True(XdsClient.MatchHostName("bar.foo.googleapis.com", pattern));
            Assert.True(XdsClient.MatchHostName("BAR.foo.googleAPIS.com", pattern));
            pattern = "*-bar.foo.googleapis.com";
            Assert.False(XdsClient.MatchHostName("bar.foo.googleapis.com", pattern));
            Assert.False(XdsClient.MatchHostName("baz-bar.foo.googleapis", pattern));
            Assert.False(XdsClient.MatchHostName("-bar.foo.googleapis.com", pattern));
            Assert.True(XdsClient.MatchHostName("baz-bar.foo.googleapis.com", pattern));
            Assert.True(XdsClient.MatchHostName("BAZ-bar.foo.googleapis.com", pattern));
        }

        [Fact]
        public void ForSuffixfixWildcard_UseXdsClientMatchHostName_VerifyHostnames()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "foo.*";
            Assert.False(XdsClient.MatchHostName("bar.googleapis.com", pattern));
            Assert.False(XdsClient.MatchHostName("bar.foo.googleapis.com", pattern));
            Assert.True(XdsClient.MatchHostName("foo.googleapis.com", pattern));
            Assert.True(XdsClient.MatchHostName("foo.com", pattern));
            Assert.True(XdsClient.MatchHostName("FOO.com", pattern));
            pattern = "foo-*";
            Assert.False(XdsClient.MatchHostName("bar-.googleapis.com", pattern));
            Assert.False(XdsClient.MatchHostName("foo.googleapis.com", pattern));
            Assert.False(XdsClient.MatchHostName("foo.googleAPIS.COM", pattern));
            Assert.False(XdsClient.MatchHostName("foo-", pattern));
            Assert.True(XdsClient.MatchHostName("foo-bar.com", pattern));
            Assert.True(XdsClient.MatchHostName("foo-.com", pattern));
            Assert.True(XdsClient.MatchHostName("foo-bar", pattern));
            Assert.True(XdsClient.MatchHostName("foo-bar.googleapis.com", pattern));
            Assert.True(XdsClient.MatchHostName("foo-bar.GOOGLEapis.com", pattern));
        }

        [Fact]
        public void ForSpecialWildcard_UseXdsClientMatchHostName_VerifyHostnames()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "*";
            Assert.True(XdsClient.MatchHostName("foo.googleapis.com", pattern));
            Assert.True(XdsClient.MatchHostName("fOO.GOOgleapis.COm", pattern));
        }

        [Fact]
        public void ForWrongPatternsWithWildcard_UseXdsClientMatchHostName_VerifyReturnFalse()
        {
            // Arrange
            // Act
            // Assert
            var pattern = "*.googleapis.*";
            Assert.False(XdsClient.MatchHostName("foo.googleapis.com", pattern));
            pattern = "foo.*.com";
            Assert.False(XdsClient.MatchHostName("foo.googleapis.com", pattern));
        }

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        [Fact]
        public void ForUnallowedHostNamesAndPatterns_UseXdsClientMatchHostName_VerifyThrowError()
        {
            // Arrange
            // Act
            // Assert
            Assert.Throws<ArgumentException>(() =>
            {
                XdsClient.MatchHostName("*", null);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsClient.MatchHostName("*", string.Empty);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsClient.MatchHostName("*", "foo.googleapis.com.");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsClient.MatchHostName("*", ".foo.googleapis.com");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsClient.MatchHostName(null, "foo.googleapis.com");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsClient.MatchHostName(string.Empty, "foo.googleapis.com");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsClient.MatchHostName("foo.googleapis.com.", "foo.googleapis.com");
            });
            Assert.Throws<ArgumentException>(() =>
            {
                XdsClient.MatchHostName(".foo.googleapis.com", "foo.googleapis.com");
            });
        }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
