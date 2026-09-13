using System.Net;
using Microsoft.Extensions.Options;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class AgentToolEndpointPolicy : IAgentToolEndpointPolicy
{
    private readonly AgentToolHttpOptions _options;

    public AgentToolEndpointPolicy(
        IOptions<AgentToolHttpOptions> options)
    {
        _options = options.Value;
    }

    public async Task<bool> IsAllowedAsync(
        Uri endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(endpoint.UserInfo))
        {
            return false;
        }

        if (endpoint.Scheme != Uri.UriSchemeHttps &&
            endpoint.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        bool localhost =
            endpoint.IsLoopback ||
            string.Equals(
                endpoint.Host,
                "localhost",
                StringComparison.OrdinalIgnoreCase);

        if (localhost)
        {
            return _options.AllowLocalhost;
        }

        // HTTP público não é permitido.
        if (endpoint.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        IPAddress[] addresses;

        try
        {
            addresses = await Dns.GetHostAddressesAsync(
                endpoint.DnsSafeHost,
                cancellationToken);
        }
        catch
        {
            return false;
        }

        if (addresses.Length == 0)
        {
            return false;
        }

        foreach (IPAddress address in addresses)
        {
            if (IsPrivateOrSpecial(address) &&
                !_options.AllowPrivateNetworks)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPrivateOrSpecial(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily ==
            System.Net.Sockets.AddressFamily.InterNetwork)
        {
            byte[] bytes = address.GetAddressBytes();

            return
                bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 &&
                    bytes[1] >= 16 &&
                    bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                bytes[0] == 0 ||
                bytes[0] >= 224;
        }

        if (address.AddressFamily ==
            System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (address.Equals(IPAddress.IPv6Any) ||
                address.Equals(IPAddress.IPv6None) ||
                address.Equals(IPAddress.IPv6Loopback))
            {
                return true;
            }

            byte[] bytes = address.GetAddressBytes();

            // fc00::/7 - Unique Local Address
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return true;
            }

            // fe80::/10 - Link-local
            if (bytes[0] == 0xFE &&
                (bytes[1] & 0xC0) == 0x80)
            {
                return true;
            }
        }

        return false;
    }
}
