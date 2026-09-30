using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// Applies the trusted-proxy allowlist to <see cref="ForwardedHeadersOptions"/>, shared by the
/// API host and the AuthServer so the two cannot drift (#928).
///
/// <para><b>What this list actually gates.</b> The forwarded-headers middleware tests the
/// IMMEDIATE PEER -- <c>Connection.RemoteIpAddress</c> -- against this allowlist before it will
/// honour <c>X-Forwarded-*</c> at all. Our immediate peer is always our own nginx on the compose
/// network, never the cloud load balancer, so the value here is the CONTAINER NETWORK range. The
/// balancer's range belongs in nginx's <c>set_real_ip_from</c> instead, and the two are not
/// interchangeable. Getting them the wrong way round silently drops every forwarded header,
/// because the peer would no longer be in the list.</para>
///
/// <para><b>Why unset means cleared rather than defaulted.</b> ASP.NET Core ships a default
/// allowlist containing only loopback, which the compose network is not in, so leaving the
/// defaults in place would drop the forwarded scheme behind the proxy and break OpenIddict's
/// transport-security check. Clearing it trusts whatever peer reaches the container -- correct on
/// a box whose only ingress is our own nginx, and the behaviour this deployment has always had.
/// Configuring the range narrows that to the network nginx is actually on, which is ANTI-14, and
/// matters the moment anything else can route to the container.</para>
///
/// <para><b>This does not change how many hops are trusted.</b> <c>ForwardLimit</c> stays at 1 and
/// is deliberately untouched here: it is the reason a forged <c>X-Forwarded-For</c> cannot choose
/// its own rate-limit bucket, because nginx appends the real address last and the middleware reads
/// right-to-left. Raising it is what would start honouring client-supplied hops.</para>
/// </summary>
public static class TrustedProxyNetworks
{
    /// <summary>
    /// Comma-separated CIDR ranges of the network our reverse proxy connects FROM. Blank or absent
    /// keeps the historical behaviour of trusting any peer.
    /// </summary>
    public const string ConfigurationKey = "App:TrustedProxyNetworks";

    /// <summary>
    /// Replaces the framework defaults with the configured ranges, or clears the allowlist entirely
    /// when nothing is configured.
    /// </summary>
    /// <exception cref="AbpException">
    /// A configured entry is not a valid CIDR. Thrown at startup rather than skipped: a silently
    /// ignored entry leaves the peer outside the allowlist, which drops every forwarded header and
    /// collapses the per-IP partitions -- the exact defect this setting exists to close. Refusing
    /// to start is the louder failure and the safer one.
    /// </exception>
    public static void Apply(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        Check.NotNull(options, nameof(options));
        Check.NotNull(configuration, nameof(configuration));

        // Cleared in both branches: the framework default is loopback-only, which our proxy is
        // never on, so leaving it would drop the headers rather than narrow who may send them.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        var configured = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        foreach (var entry in configured.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IPNetwork.TryParse(entry, out var network))
            {
                throw new AbpException(
                    $"{ConfigurationKey} contains '{entry}', which is not a valid CIDR range. " +
                    "Expected a comma-separated list such as '172.16.0.0/12'. This is the network " +
                    "the reverse proxy connects FROM, not the load balancer's public range.");
            }

            options.KnownIPNetworks.Add(network);
        }
    }
}
