using Dynasland.Core.Services;

namespace Dynasland.Tests.Core;

public class SensitiveContentFilterTests
{
    [Theory]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("5500-0000-0000-0004")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nabc")]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz0123456789")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U")]
    public void Detects_sensitive_text(string text) => Assert.True(SensitiveContentFilter.IsLikelySensitive(text));

    [Theory]
    [InlineData("Olá, tudo bem?")]
    [InlineData("4111 1111 1111 1112")]
    [InlineData("12345")]
    [InlineData("https://github.com/danilloteeo/dynasland/releases")]
    [InlineData("")]
    public void Keeps_ordinary_text(string text) => Assert.False(SensitiveContentFilter.IsLikelySensitive(text));
}
