using System.Net;
using System.Net.Sockets;

namespace CompanyHQ.Services.Rss.Services;

/// <summary>
/// The two things the feed fetcher asks of the operating system: name resolution and a TCP
/// connection to an address it has already approved.
/// </summary>
/// <remarks>
/// It is a seam for tests, not an extension point. Everything that decides whether a request is
/// safe happens above it and is the same in a test as in production: a test replaces only where a
/// name resolves to and where an approved address really connects, so it can serve fixtures from
/// this machine while the fetcher still believes it is talking to a public host.
/// </remarks>
public interface IRssNetwork
{
    /// <summary>Every address a host name resolves to. Empty when it does not resolve.</summary>
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);

    /// <summary>Opens a connection to exactly this address. The fetcher never passes a name here.</summary>
    Task<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken);
}

/// <summary>The real network: the system resolver and a plain TCP socket.</summary>
public sealed class SystemRssNetwork : IRssNetwork
{
    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            return await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            // An unknown name is an unreachable publisher, not a fault here.
            return [];
        }
    }

    public async Task<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken);

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
