using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Contributions;

/// Keeps one idempotency key for an unchanged draft and creates a new key for new content.
public sealed class ContributionRequestIdentity
{
  private readonly Dictionary<string, Guid> keys = new(StringComparer.Ordinal);

  public string KeyFor(string surface, string canonicalIntent)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(surface);
    ArgumentNullException.ThrowIfNull(canonicalIntent);
    var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalIntent)));
    var scope = $"{surface}:{fingerprint}";
    if (!keys.TryGetValue(scope, out var key))
    {
      key = Guid.NewGuid();
      keys[scope] = key;
    }
    return key.ToString();
  }

  public void Complete(string surface, string canonicalIntent) =>
      keys.Remove($"{surface}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalIntent)))}");
}

public sealed record ContributionAdmission(
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("retry_after_seconds")] int? RetryAfterSeconds = null);

public sealed record ContributionStatusResponse(
    [property: JsonPropertyName("admission")] ContributionAdmission Admission,
    [property: JsonPropertyName("contribution_status")] ContributionStatus ContributionStatus,
    [property: JsonPropertyName("daily_quota")] ContributionDailyQuota DailyQuota);

public sealed record ContributionStatus(
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("gated_until")] DateTimeOffset? GatedUntil = null);

public sealed record ContributionDailyQuota(
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("used")] int Used);
