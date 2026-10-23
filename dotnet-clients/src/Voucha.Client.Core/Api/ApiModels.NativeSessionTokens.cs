using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record NativeEmailTokenResponse(
    [property: JsonPropertyName("email_address"), JsonRequired] string EmailAddress);

public sealed record NativeEmailMfaResponse(
    [property: JsonPropertyName("login_attempt_id"), JsonRequired] string LoginAttemptId,
    [property: JsonPropertyName("mfa_required"), JsonRequired, JsonConverter(typeof(TrueApiBooleanConverter))] bool MfaRequired);

public sealed record NativeDevicePayload([property: JsonRequired] string Did);

public sealed record NativeSessionPayload(
    [property: JsonRequired] string Did,
    [property: JsonRequired] string Sid,
    [property: JsonRequired] string Uid);

public sealed record NativeDeviceToken([property: JsonRequired] NativeDevicePayload Payload, [property: JsonRequired] string Token);

public sealed record NativeSessionToken([property: JsonRequired] NativeSessionPayload Payload, [property: JsonRequired] string Token);

public enum NativeAuthenticatedEntityType
{
  [JsonStringEnumMemberName("user")]
  User,
}

public sealed record NativeEmailAuthenticatedUser(
    [property: JsonRequired] string Id,
    [property: JsonPropertyName("__entity_type"), JsonRequired] NativeAuthenticatedEntityType EntityType,
    [property: JsonPropertyName("account_type"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? AccountType,
    [property: JsonPropertyName("email_address")] string? EmailAddress,
    IReadOnlyList<string>? Roles);

public sealed record NativeEmailAuthenticatedResponse(
    [property: JsonRequired] string Did,
    [property: JsonRequired] NativeDeviceToken Dt,
    [property: JsonRequired] NativeSessionPayload Session,
    [property: JsonRequired] string Sid,
    [property: JsonRequired] NativeSessionToken St,
    [property: JsonRequired] string Uid,
    [property: JsonRequired] NativeEmailAuthenticatedUser User);

public enum NativeSessionTokenType
{
  [JsonStringEnumMemberName("Bearer")]
  Bearer,
}

public sealed record NativeSessionMcpTokenResponse(
    [property: JsonPropertyName("access_token"), JsonRequired] string AccessToken,
    [property: JsonPropertyName("expires_in"), JsonRequired] double ExpiresIn,
    [property: JsonRequired] string Resource,
    [property: JsonRequired] string Scope,
    [property: JsonPropertyName("token_type"), JsonRequired] NativeSessionTokenType TokenType);
