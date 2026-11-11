using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

/// <summary>Fresh-confirmation methods available to the signed-in account.</summary>
public sealed record ReAuthMethodsResponse(
    [property: JsonPropertyName("has_email")] bool HasEmail,
    [property: JsonPropertyName("has_passkey")] bool HasPasskey,
    [property: JsonPropertyName("has_totp")] bool HasTotp,
    [property: JsonPropertyName("providers")] IReadOnlyList<string> Providers);

/// <summary>One-use proof returned by every successful re-auth verification route.</summary>
public sealed record ReAuthTokenResponse(
    [property: JsonPropertyName("re_auth_token")] string ReAuthToken);

public sealed record ReAuthPasskeyCredential(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("transports")] IReadOnlyList<string>? Transports);

public sealed record ReAuthPasskeyOptions(
    [property: JsonPropertyName("challenge")] string Challenge,
    [property: JsonPropertyName("rpId")] string RpId,
    [property: JsonPropertyName("timeout")] int? Timeout,
    [property: JsonPropertyName("userVerification")] string? UserVerification,
    [property: JsonPropertyName("allowCredentials")] IReadOnlyList<ReAuthPasskeyCredential>? AllowCredentials);

public sealed record ReAuthPasskeyOptionsResponse(
    [property: JsonPropertyName("flow_id")] string FlowId,
    [property: JsonPropertyName("options")] ReAuthPasskeyOptions Options);

/// <summary>Server challenge for an SDK provider (Apple or Google); the assertion must carry the nonce.</summary>
public sealed record ReAuthProviderChallengeResponse(
    [property: JsonPropertyName("flow_id")] string FlowId,
    [property: JsonPropertyName("nonce")] string Nonce,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);

public sealed record NativeRefreshedSessionClaims(
    [property: JsonPropertyName("client")] string Client,
    [property: JsonPropertyName("did")] string Did,
    [property: JsonPropertyName("rca")] long Rca,
    [property: JsonPropertyName("rol")] IReadOnlyList<string> Rol,
    [property: JsonPropertyName("sca")] long Sca,
    [property: JsonPropertyName("sid")] string Sid,
    [property: JsonPropertyName("uid")] string? Uid);

public sealed record NativeRefreshedSession(
    [property: JsonPropertyName("did")] string Did,
    [property: JsonPropertyName("dt")] string Dt,
    [property: JsonPropertyName("dte")] int Dte,
    [property: JsonPropertyName("secure")] bool Secure,
    [property: JsonPropertyName("session")] NativeRefreshedSessionClaims Session,
    [property: JsonPropertyName("sid")] string Sid,
    [property: JsonPropertyName("st")] string St,
    [property: JsonPropertyName("ste")] int Ste,
    [property: JsonPropertyName("uid")] string? Uid);

/// <summary>Result of rotating `PATCH /api/v1/session`: both replacement tokens and their lifetimes.</summary>
public sealed record NativeSessionRefreshResponse(
    [property: JsonPropertyName("session")] NativeRefreshedSession Session);

/// <summary>403 body of a mutation that needs a bot challenge before it can proceed.</summary>
public sealed record BotChallengeRequiredResponse(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message)
{
  public const string RequiredCode = "BOT_CHALLENGE_REQUIRED";
}
