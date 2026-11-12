using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Auth;

public sealed record AuthResponse(
    [property: JsonPropertyName("mfa_required")] bool? MfaRequired,
    [property: JsonPropertyName("login_attempt_id")] string? LoginAttemptId,
    [property: JsonPropertyName("dt")] Voucha.Client.Core.Api.NativeSessionToken? Dt = null,
    [property: JsonPropertyName("st")] Voucha.Client.Core.Api.NativeSessionToken? St = null);

public sealed record PasskeyAuthenticationOptionsResponse(
    [property: JsonPropertyName("options")] PasskeyAuthenticationOptions Options);
