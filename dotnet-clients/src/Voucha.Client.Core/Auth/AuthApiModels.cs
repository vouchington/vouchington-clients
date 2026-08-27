using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Auth;

public sealed record AuthResponse(
    [property: JsonPropertyName("mfa_required")] bool? MfaRequired,
    [property: JsonPropertyName("login_attempt_id")] string? LoginAttemptId);

public sealed record PasskeyAuthenticationOptionsResponse(
    [property: JsonPropertyName("options")] PasskeyAuthenticationOptions Options);
