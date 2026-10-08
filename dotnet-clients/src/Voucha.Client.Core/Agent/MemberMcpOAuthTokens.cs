using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Agent;

public sealed record MemberMcpOAuthTokens(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("token_type")] string TokenType)
{
  /// <summary>Persisted alongside the tokens so restart does not reset the expiry window.</summary>
  [JsonPropertyName("acquired_at")]
  public DateTimeOffset AcquiredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Windows adapter must use Credential Locker or current-user DPAPI; never settings files.</summary>
public interface IMemberMcpOAuthTokenStore
{
  Task<MemberMcpOAuthTokens?> LoadAsync(string accountId);
  Task SaveAsync(string accountId, MemberMcpOAuthTokens tokens);
  Task ClearAsync(string accountId);
}
