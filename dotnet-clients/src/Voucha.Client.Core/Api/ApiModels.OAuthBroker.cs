using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum OAuthBrokerProvider
{
  Facebook,
  X,
  Github,
}

public enum OAuthAuthorizationPurpose
{
  Authenticate,
  Connect,
}

public sealed record OAuthBrokerModes(
    [property: JsonPropertyName("web")] bool Web,
    [property: JsonPropertyName("native")] bool Native);

public sealed record OAuthBrokerCapability(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("modes")] OAuthBrokerModes Modes,
    [property: JsonPropertyName("purposes")] IReadOnlyList<OAuthAuthorizationPurpose> Purposes)
{
  public bool SupportsNative(OAuthAuthorizationPurpose purpose) =>
      Version == 1 && Modes.Native && Purposes.Contains(purpose);
}

public sealed record OAuthBrokerCapabilities(
    [property: JsonPropertyName("facebook")] OAuthBrokerCapability Facebook,
    [property: JsonPropertyName("x")] OAuthBrokerCapability X,
    [property: JsonPropertyName("github")] OAuthBrokerCapability Github)
{
  public OAuthBrokerCapability this[OAuthBrokerProvider provider] => provider switch
  {
    OAuthBrokerProvider.Facebook => Facebook,
    OAuthBrokerProvider.X => X,
    OAuthBrokerProvider.Github => Github,
    _ => throw new ArgumentOutOfRangeException(nameof(provider)),
  };
}

public sealed record OAuthProvidersResponse(
    [property: JsonPropertyName("providers")] IReadOnlyList<string> Providers,
    [property: JsonPropertyName("broker_capabilities")] OAuthBrokerCapabilities BrokerCapabilities);

public sealed record BeginOAuthAuthorizationResponse(
    [property: JsonPropertyName("flow_id")] string FlowId,
    [property: JsonPropertyName("redirect_url")] Uri RedirectUrl,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);

public sealed record OAuthAccountInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email_address")] string? EmailAddress);

public enum OAuthCompletionKind
{
  Pending,
  Authenticated,
  MfaRequired,
  Connected,
}

[JsonConverter(typeof(OAuthCompletionResponseConverter))]
public sealed record OAuthCompletionResponse(
    OAuthCompletionKind Kind,
    User? User = null,
    string? LoginAttemptId = null,
    OAuthAccountInfo? OAuthAccount = null);

internal sealed class OAuthCompletionResponseConverter : JsonConverter<OAuthCompletionResponse>
{
  public override OAuthCompletionResponse Read(
      ref Utf8JsonReader reader,
      Type typeToConvert,
      JsonSerializerOptions options)
  {
    using var document = JsonDocument.ParseValue(ref reader);
    var root = document.RootElement;
    var pending = root.TryGetProperty("status", out var status) && status.GetString() == "pending";
    var hasUser = root.TryGetProperty("user", out var user);
    var mfa = root.TryGetProperty("mfa_required", out var mfaRequired) && mfaRequired.GetBoolean();
    var hasAccount = root.TryGetProperty("oauth_account", out var account);
    if ((pending ? 1 : 0) + (hasUser ? 1 : 0) + (mfa ? 1 : 0) + (hasAccount ? 1 : 0) != 1)
    {
      throw new JsonException("OAuth completion must contain exactly one outcome.");
    }

    if (pending) return new(OAuthCompletionKind.Pending);
    if (hasUser)
    {
      return new(
          OAuthCompletionKind.Authenticated,
          User: user.Deserialize<User>(options) ?? throw new JsonException("OAuth user is missing."));
    }
    if (hasAccount)
    {
      return new(
          OAuthCompletionKind.Connected,
          OAuthAccount: account.Deserialize<OAuthAccountInfo>(options) ??
              throw new JsonException("OAuth account is missing."));
    }

    var loginAttemptId = root.TryGetProperty("login_attempt_id", out var attempt)
        ? attempt.GetString()
        : null;
    return string.IsNullOrWhiteSpace(loginAttemptId)
        ? throw new JsonException("OAuth MFA completion is missing login_attempt_id.")
        : new(OAuthCompletionKind.MfaRequired, LoginAttemptId: loginAttemptId);
  }

  public override void Write(
      Utf8JsonWriter writer,
      OAuthCompletionResponse value,
      JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    switch (value.Kind)
    {
      case OAuthCompletionKind.Pending:
        writer.WriteString("status", "pending");
        break;
      case OAuthCompletionKind.Authenticated:
        writer.WritePropertyName("user");
        JsonSerializer.Serialize(writer, value.User, options);
        break;
      case OAuthCompletionKind.MfaRequired:
        writer.WriteString("login_attempt_id", value.LoginAttemptId);
        writer.WriteBoolean("mfa_required", true);
        break;
      case OAuthCompletionKind.Connected:
        writer.WritePropertyName("oauth_account");
        JsonSerializer.Serialize(writer, value.OAuthAccount, options);
        break;
      default:
        throw new JsonException("OAuth completion outcome is invalid.");
    }
    writer.WriteEndObject();
  }
}
