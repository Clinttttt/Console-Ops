using System.Net;
using System.Net.Sockets;

namespace ConsoleOps.Infrastructure.Integrations.ApplicationMonitoring;

internal static class ProbeHttpMessageHandlerFactory
{
    public static SocketsHttpHandler Create(IReadOnlySet<string> allowedPrivateHosts) => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectCallback = (context, cancellationToken) =>
            ConnectAsync(context.DnsEndPoint, allowedPrivateHosts, cancellationToken),
        ConnectTimeout = TimeSpan.FromSeconds(5),
        MaxConnectionsPerServer = 8,
        MaxResponseHeadersLength = 16,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        UseCookies = false,
        UseProxy = false,
        ActivityHeadersPropagator = null
    };

    private static async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endpoint,
        IReadOnlySet<string> allowedPrivateHosts,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses = IPAddress.TryParse(endpoint.Host, out IPAddress? literalAddress)
            ? [literalAddress]
            : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);

        if (addresses.Length == 0
            || addresses.Any(address =>
                !OutboundAddressPolicy.IsAllowed(endpoint.Host, address, allowedPrivateHosts)))
        {
            throw new HttpRequestException("The probe target is not permitted.");
        }

        SocketException? lastFailure = null;

        foreach (IPAddress address in addresses)
        {
            Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, endpoint.Port),
                    cancellationToken);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                lastFailure = exception;
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("The probe target could not be reached.", lastFailure);
    }
}
