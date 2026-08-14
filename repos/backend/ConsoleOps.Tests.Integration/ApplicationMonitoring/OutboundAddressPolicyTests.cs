using System.Net;
using ConsoleOps.Infrastructure.Integrations.ApplicationMonitoring;

namespace ConsoleOps.Tests.Integration.ApplicationMonitoring;

public sealed class OutboundAddressPolicyTests
{
    private static readonly IReadOnlySet<string> NoPrivateHosts =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("0.1.2.3", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("198.51.100.1", false)]
    [InlineData("203.0.113.1", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("ff02::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2002:7f00:1::", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    public void IsAllowed_ClassifiesPublicAndSpecialPurposeAddresses(
        string value,
        bool expected)
    {
        bool result = OutboundAddressPolicy.IsAllowed(
            "application.example",
            IPAddress.Parse(value),
            NoPrivateHosts);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsAllowed_RequiresAnExactNormalizedPrivateHostAllowlistEntry()
    {
        HashSet<string> allowedHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "local-api"
        };
        IPAddress privateAddress = IPAddress.Parse("192.168.1.20");

        Assert.True(OutboundAddressPolicy.IsAllowed("LOCAL-API.", privateAddress, allowedHosts));
        Assert.False(OutboundAddressPolicy.IsAllowed("other-local-api", privateAddress, allowedHosts));
    }

    [Fact]
    public void Create_DisablesRedirectsCookiesProxiesAndAutomaticDecompression()
    {
        using SocketsHttpHandler handler = ProbeHttpMessageHandlerFactory.Create(NoPrivateHosts);

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
        Assert.False(handler.UseProxy);
        Assert.Equal(DecompressionMethods.None, handler.AutomaticDecompression);
        Assert.NotNull(handler.ConnectCallback);
        Assert.Null(handler.ActivityHeadersPropagator);
        Assert.Equal(8, handler.MaxConnectionsPerServer);
    }
}
