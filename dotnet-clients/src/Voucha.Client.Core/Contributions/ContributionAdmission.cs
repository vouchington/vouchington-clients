using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Voucha.Client.Core.Contributions;

/// Keeps one idempotency key for an unchanged draft and creates a new key for new content.
public sealed class ContributionRequestIdentity
{
  private readonly object sync = new();
  private readonly Dictionary<string, CurrentIdentity> identities = new(StringComparer.Ordinal);

  public string KeyFor(string surface, string canonicalIntent)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(surface);
    ArgumentNullException.ThrowIfNull(canonicalIntent);
    var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalIntent)));
    lock (sync)
    {
      if (!identities.TryGetValue(surface, out var identity) || identity.Fingerprint != fingerprint)
      {
        identity = new CurrentIdentity(fingerprint, Guid.NewGuid());
        identities[surface] = identity;
      }
      return identity.Key.ToString();
    }
  }

  public void Complete(string surface, string canonicalIntent)
  {
    var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalIntent)));
    lock (sync)
    {
      if (identities.TryGetValue(surface, out var identity) && identity.Fingerprint == fingerprint)
      {
        identities.Remove(surface);
      }
    }
  }

  public static string CanonicalIntent(object body)
  {
    var node = JsonNode.Parse(JsonSerializer.Serialize(body, Api.VouchaApiJson.Options))!.AsObject();
    node.Remove("cf_turnstile_response");
    node.Remove("recaptcha_token");
    node.Remove("hp_website");
    node.Remove("hp_phone");
    return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
  }

  private sealed record CurrentIdentity(string Fingerprint, Guid Key);
}

public sealed record ContributionAdmission(
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("retry_after_seconds")] int? RetryAfterSeconds = null);

public sealed record ContributionStatusResponse(
    [property: JsonPropertyName("admission")] ContributionAdmission Admission,
    [property: JsonPropertyName("contribution_status")] ContributionStatus ContributionStatus,
    [property: JsonPropertyName("daily_quota")] ContributionDailyQuota DailyQuota,
    [property: JsonPropertyName("action_limit")] ContributionActionLimitStatus? ActionLimit = null);

public sealed record ContributionStatus(
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("gated_until")] DateTimeOffset? GatedUntil = null);

public sealed record ContributionDailyQuota(
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("used")] int Used);

public sealed record ContributionActionLimitStatus(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("daily_window")] ContributionLimitUsage DailyWindow,
    [property: JsonPropertyName("short_window")] ContributionLimitUsage ShortWindow,
    [property: JsonPropertyName("tier")] string Tier);

public sealed record ContributionLimitUsage(
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("used")] int Used,
    [property: JsonPropertyName("window_seconds")] int WindowSeconds);
