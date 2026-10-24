using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record NativeEmailTokenResponse(
    [property: JsonPropertyName("email_address")] string EmailAddress);

public sealed record NativeSessionTokenPayload(
    [property: JsonPropertyName("did")] string Did,
    [property: JsonPropertyName("sid")] string? Sid,
    [property: JsonPropertyName("uid")] string? Uid);

public sealed record NativeSessionToken(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("payload")] NativeSessionTokenPayload Payload);

public sealed record NativeSessionIdentity(
    [property: JsonPropertyName("did")] string Did,
    [property: JsonPropertyName("sid")] string Sid,
    [property: JsonPropertyName("uid")] string Uid);

public sealed record NativeLoginUser(
    [property: JsonPropertyName("__entity_type")] string EntityType,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email_address")] string? EmailAddress,
    [property: JsonPropertyName("account_type")] string? AccountType,
    [property: JsonPropertyName("roles")] IReadOnlyList<string> Roles);

/// <summary>Body-token result of the native email login route when sign-in completes.</summary>
public sealed record NativeEmailLoginAuthenticatedResponse(
    [property: JsonPropertyName("did")] string Did,
    [property: JsonPropertyName("sid")] string Sid,
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("dt")] NativeSessionToken Dt,
    [property: JsonPropertyName("st")] NativeSessionToken St,
    [property: JsonPropertyName("session")] NativeSessionIdentity Session,
    [property: JsonPropertyName("user")] NativeLoginUser User);

/// <summary>Result of the native email login route when multi-factor verification is required.</summary>
public sealed record NativeEmailLoginMfaResponse(
    [property: JsonPropertyName("login_attempt_id")] string LoginAttemptId,
    [property: JsonPropertyName("mfa_required")] bool MfaRequired);

public sealed record NativeSessionMcpTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("token_type")] string TokenType);

public sealed record AuthSessionMcpConnection(
    [property: JsonPropertyName("grant_id")] string GrantId,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("attestation_status")] string AttestationStatus,
    [property: JsonPropertyName("last_minted_at")] DateTimeOffset LastMintedAt);

public sealed record RequestNativeEmailOtpBody(
    [property: JsonPropertyName("emailAddress")] string Email);

public sealed record MintNativeSessionMcpTokenBody(
    [property: JsonPropertyName("client_app")] string ClientApp);
