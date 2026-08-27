using System.Net;
using System.Net.Sockets;

namespace Voucha.Client.Core.Chat;

/// <summary>
/// Shared private/local-network range policy for local-LLM endpoints. <see cref="LocalLLMEndpointProfile"/>
/// applies this to the configured hostname when a profile is saved; <see cref="LocalLLMConnectionPinning"/>
/// re-applies it to the actually-resolved peer address at connect time so a hostname that only looked
/// private when it was typed cannot silently reach a public address over cleartext later. The hostname
/// overload mirrors <c>LocalLLMEndpointProfile.isAllowedCleartextHost</c> in
/// <c>swift-clients/core/Sources/VouchaCore/LocalLLMEndpointProfile.swift</c>; the shared corpus at
/// <c>api-fixtures/v1/local-llm-endpoint-policy.json</c> pins both platforms to the same verdicts there.
/// HTTP is allowed only for loopback, RFC 1918, IPv4/IPv6 link-local, IPv6 ULA, localhost, ASCII
/// <c>.local</c>, and single-label LAN names. RFC 6598 <c>100.64.0.0/10</c> is not private (#9474).
/// </summary>
internal static class LocalLLMNetworkPolicy
{
  internal static bool IsPrivateNetworkHost(string host)
  {
    ArgumentNullException.ThrowIfNull(host);

    // A successful parse must be terminal. IPAddress.TryParse already accepts the bracketed
    // form Uri.Host returns for IPv6 (e.g. "[fc00::8]"), so the only real bug was letting the
    // single-label branch below run first and misclassify every bare IPv6 literal (colons, no
    // dots) as private-by-default before a parse was ever attempted.
    if (IPAddress.TryParse(host, out var address))
    {
      // IPv4-mapped IPv6 literals (::ffff:a.b.c.d) are rejected outright regardless of the
      // mapped address's own range, for a saved/typed hostname: the scope is ambiguous, and
      // Swift's validator already rejects every ::ffff:... form unconditionally. This
      // deliberately differs from the IsPrivateNetworkHost(IPAddress) overload below: a real DNS
      // resolution can legitimately produce ::ffff:127.0.0.1 for "localhost", and
      // LocalLLMConnectionPinning's connect-time fallback must keep accepting that candidate.
      if (address.IsIPv4MappedToIPv6) return false;

      // A successful parse is terminal: return the range verdict outright. Falling through to
      // the single-label branch would re-accept a public IPv6 literal, since it has no dots.
      return IsPrivateNetworkHost(address);
    }

    return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
        !host.Contains('.', StringComparison.Ordinal);
  }

  internal static bool IsPrivateNetworkHost(IPAddress address)
  {
    ArgumentNullException.ThrowIfNull(address);
    if (IPAddress.IsLoopback(address)) return true;
    var bytes = address.GetAddressBytes();
    return address.AddressFamily switch
    {
      AddressFamily.InterNetwork => bytes[0] == 10 || bytes[0] == 127 ||
          (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
          (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 169 && bytes[1] == 254),
      AddressFamily.InterNetworkV6 => (bytes[0] & 0xfe) == 0xfc ||
          (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80),
      _ => false,
    };
  }
}
