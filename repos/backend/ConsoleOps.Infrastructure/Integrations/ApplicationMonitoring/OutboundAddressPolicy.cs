using System.Net;

namespace ConsoleOps.Infrastructure.Integrations.ApplicationMonitoring;

internal static class OutboundAddressPolicy
{
    public static bool IsAllowed(
        string host,
        IPAddress address,
        IReadOnlySet<string> allowedPrivateHosts)
    {
        if (allowedPrivateHosts.Contains(NormalizeHost(host)))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork => IsPublicIpv4(address),
            System.Net.Sockets.AddressFamily.InterNetworkV6 => IsPublicIpv6(address),
            _ => false
        };
    }

    public static string NormalizeHost(string host) =>
        host.Trim().TrimEnd('.').ToLowerInvariant();

    private static bool IsPublicIpv4(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        byte first = bytes[0];
        byte second = bytes[1];
        byte third = bytes[2];

        return first != 0
            && first != 10
            && !(first == 100 && second is >= 64 and <= 127)
            && first != 127
            && !(first == 169 && second == 254)
            && !(first == 172 && second is >= 16 and <= 31)
            && !(first == 192 && second == 0 && third == 0)
            && !(first == 192 && second == 0 && third == 2)
            && !(first == 192 && second == 88 && third == 99)
            && !(first == 192 && second == 168)
            && !(first == 198 && second is 18 or 19)
            && !(first == 198 && second == 51 && third == 100)
            && !(first == 203 && second == 0 && third == 113)
            && first < 224;
    }

    private static bool IsPublicIpv6(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)
            || address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.IsIPv6SiteLocal)
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();

        // Only global-unicast space is eligible, with special-purpose ranges removed below.
        if (bytes[0] is < 0x20 or > 0x3f)
        {
            return false;
        }

        bool isIetfSpecialPurpose = bytes[0] == 0x20
            && bytes[1] == 0x01
            && bytes[2] <= 0x01;
        bool isDocumentation = bytes[0] == 0x20
            && bytes[1] == 0x01
            && bytes[2] == 0x0d
            && bytes[3] == 0xb8;
        bool isSixToFour = bytes[0] == 0x20 && bytes[1] == 0x02;
        bool isExtendedDocumentation = bytes[0] == 0x3f
            && bytes[1] == 0xff
            && (bytes[2] & 0xf0) == 0;

        return !isIetfSpecialPurpose
            && !isDocumentation
            && !isSixToFour
            && !isExtendedDocumentation;
    }
}
