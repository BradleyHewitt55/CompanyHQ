using System.Net;
using System.Net.Sockets;

namespace CompanyHQ.Services.Rss.Services;

/// <summary>
/// Decides whether an address is one on the public internet. Everything else — this machine, the
/// local network, a cloud metadata endpoint, a documentation or reserved range — is never a place
/// a feed can live, and is the destination a forged feed address is trying to reach.
/// </summary>
internal static class PublicAddress
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false
        };
    }

    private static bool IsPublicV4(byte[] b) => (b[0], b[1], b[2]) switch
    {
        (0, _, _) => false,                       // 0.0.0.0/8 "this network"
        (10, _, _) => false,                      // 10.0.0.0/8 private
        (100, >= 64 and <= 127, _) => false,      // 100.64.0.0/10 carrier-grade NAT
        (127, _, _) => false,                     // 127.0.0.0/8 loopback
        (169, 254, _) => false,                   // 169.254.0.0/16 link-local, cloud metadata
        (172, >= 16 and <= 31, _) => false,       // 172.16.0.0/12 private
        (192, 0, 0) => false,                     // 192.0.0.0/24 protocol assignments
        (192, 0, 2) => false,                     // 192.0.2.0/24 documentation
        (192, 88, 99) => false,                   // 192.88.99.0/24 6to4 relay
        (192, 168, _) => false,                   // 192.168.0.0/16 private
        (198, 18 or 19, _) => false,              // 198.18.0.0/15 benchmarking
        (198, 51, 100) => false,                  // 198.51.100.0/24 documentation
        (203, 0, 113) => false,                   // 203.0.113.0/24 documentation
        ( >= 224, _, _) => false,                 // 224.0.0.0/4 multicast, 240.0.0.0/4 reserved, broadcast
        _ => true
    };

    private static bool IsPublicV6(IPAddress address)
    {
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
            || address.IsIPv6UniqueLocal || address.Equals(IPAddress.IPv6Loopback) || address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        var b = address.GetAddressBytes();

        // ::/96 — unspecified, and the deprecated IPv4-compatible form that hides a v4 address.
        if (b.AsSpan(0, 12).IndexOfAnyExcept((byte)0) < 0)
        {
            return false;
        }

        // 64:ff9b::/96 NAT64 and 2002::/16 6to4 both carry an IPv4 address; judge that address.
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b)
        {
            return b.AsSpan(4, 8).IndexOfAnyExcept((byte)0) < 0 && IsPublicV4(b[12..16]);
        }

        if (b[0] == 0x20 && b[1] == 0x02)
        {
            return IsPublicV4(b[2..6]);
        }

        return (b[0], b[1]) switch
        {
            (0x01, 0x00) => false,                    // 100::/64 discard
            (0x20, 0x01) when b[2] < 0x02 => false,   // 2001::/23 protocol assignments, incl. Teredo
            (0x20, 0x01) when b[2] == 0x0d && b[3] == 0xb8 => false, // 2001:db8::/32 documentation
            (0x3f, 0xff) when (b[2] & 0xf0) == 0 => false, // 3fff::/20 documentation
            (0x20, 0x01) when b[2] == 0x00 && (b[3] & 0xf0) == 0x20 => false, // 2001:20::/28 ORCHIDv2
            // Global unicast is 2000::/3; nothing outside it is routable on the public internet.
            _ => (b[0] & 0xe0) == 0x20
        };
    }
}
