using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Voucha.Client.Core.Api;

public sealed class PublicKeyPinningPolicy
{
  public static PublicKeyPinningPolicy VouchaDefault { get; } =
      new(["voucha.ai", "staging.voucha.ai"], []);

  private readonly HashSet<string> eligibleHosts;
  private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> pinsByHost;

  public PublicKeyPinningPolicy(IEnumerable<string> eligibleHosts, IEnumerable<PublicKeyPin> pins)
  {
    this.eligibleHosts = new HashSet<string>(
        eligibleHosts.Select(NormalizeHost),
        StringComparer.Ordinal);
    pinsByHost = pins
        .GroupBy(pin => NormalizeHost(pin.Host), StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlySet<string>)new HashSet<string>(
                group.Select(pin => pin.SpkiSha256Base64),
                StringComparer.Ordinal),
            StringComparer.Ordinal);
  }

  /// Indicates whether any host has pins configured; used to audit zero-pin rollout defaults.
  public bool HasConfiguredPins => pinsByHost.Values.Any(pins => pins.Count > 0);

  public bool IsEligibleHost(string host) =>
      TryNormalizeHost(host, out var normalizedHost) &&
      eligibleHosts.Contains(normalizedHost);

  public bool RequiresPinning(Uri url)
  {
    ArgumentNullException.ThrowIfNull(url);
    if (!url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        !TryNormalizeHost(url.Host, out var normalizedHost))
    {
      return false;
    }

    return eligibleHosts.Contains(normalizedHost) &&
        pinsByHost.TryGetValue(normalizedHost, out var pins) &&
        pins.Count > 0;
  }

  public PublicKeyPinValidationResult Validate(string host, string spkiSha256Base64)
  {
    if (!TryNormalizeHost(host, out var normalizedHost) ||
        !eligibleHosts.Contains(normalizedHost) ||
        !pinsByHost.TryGetValue(normalizedHost, out var pins) ||
        pins.Count == 0)
    {
      return PublicKeyPinValidationResult.NotPinned;
    }

    return pins.Contains(spkiSha256Base64)
        ? PublicKeyPinValidationResult.Accepted
        : PublicKeyPinValidationResult.Rejected;
  }

  public bool ShouldAcceptCertificate(
      string host,
      X509Certificate? certificate,
      SslPolicyErrors errors)
  {
    if (errors != SslPolicyErrors.None)
    {
      return false;
    }

    if (!TryNormalizeHost(host, out var normalizedHost))
    {
      return true;
    }

    if (!pinsByHost.TryGetValue(normalizedHost, out var pins) || pins.Count == 0)
    {
      return true;
    }

    var spkiHash = TryGetLeafSpkiSha256Base64(certificate);
    return spkiHash is not null &&
        Validate(normalizedHost, spkiHash) is PublicKeyPinValidationResult.Accepted;
  }

  public static string? TryGetLeafSpkiSha256Base64(X509Certificate? certificate)
  {
    if (certificate is null)
    {
      return null;
    }

    try
    {
      if (certificate is X509Certificate2 certificate2)
      {
        return ComputeSpkiSha256Base64(certificate2);
      }

      using var ownedCertificate = new X509Certificate2(certificate);
      return ComputeSpkiSha256Base64(ownedCertificate);
    }
    catch (Exception ex) when (ex is NotSupportedException or CryptographicException)
    {
      return null;
    }
  }

  private static string ComputeSpkiSha256Base64(X509Certificate2 certificate) =>
      Convert.ToBase64String(SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo()));

  private static string NormalizeHost(string host) =>
      !string.IsNullOrWhiteSpace(host)
          ? host.Trim().ToUpperInvariant()
          : throw new ArgumentException("Value cannot be empty.", nameof(host));

  private static bool TryNormalizeHost(string host, out string normalizedHost)
  {
    if (string.IsNullOrWhiteSpace(host))
    {
      normalizedHost = string.Empty;
      return false;
    }

    normalizedHost = host.Trim().ToUpperInvariant();
    return true;
  }
}
