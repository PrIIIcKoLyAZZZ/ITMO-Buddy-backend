using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ITMO.Buddy.Api.Configuration;

internal static class ReverseProxyConfiguration
{
    public static bool AddTrustedForwarding(this WebApplicationBuilder builder)
    {
        if (builder.Configuration.GetValue<bool>("FORWARDEDHEADERS_ENABLED") ||
            builder.Configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED"))
        {
            throw new InvalidOperationException("Configure explicit trust under ReverseProxy instead of enabling automatic forwarding.");
        }

        var section = builder.Configuration.GetSection("ReverseProxy");
        var limit = section.GetValue("ForwardLimit", 1);
        if (limit is < 1 or > 10)
        {
            throw new InvalidOperationException("ReverseProxy:ForwardLimit must be between 1 and 10.");
        }

        var proxies = (section.GetSection("KnownProxies").Get<string[]>() ?? []).Select(ParseAddress).ToArray();
        var networks = (section.GetSection("KnownNetworks").Get<string[]>() ?? []).Select(ParseNetwork).ToArray();
        if (proxies.Length == 0 && networks.Length == 0)
        {
            return false;
        }

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = limit;
            options.RequireHeaderSymmetry = true;

            // Replace implicit loopback trust with the nonempty explicit allowlist above.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        return true;
    }

    private static IPAddress ParseAddress(string value)
    {
        if (!IPAddress.TryParse(value, out var address) ||
            address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            throw new InvalidOperationException("ReverseProxy:KnownProxies must contain explicit IP addresses.");
        }

        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    private static System.Net.IPNetwork ParseNetwork(string value)
    {
        if (!System.Net.IPNetwork.TryParse(value, out var network) || network.PrefixLength == 0 ||
            network.BaseAddress.IsIPv4MappedToIPv6)
        {
            throw new InvalidOperationException("ReverseProxy:KnownNetworks must contain restricted IPv4 or IPv6 CIDRs.");
        }

        return network;
    }
}
