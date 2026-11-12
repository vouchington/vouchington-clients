using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ReAuthCodeBody([property: JsonPropertyName("code")] string Code);

public sealed record ReAuthPasskeyOptionsBody(
    [property: JsonPropertyName("passkey_id")] string PasskeyId);

/// <summary>`response` is the platform WebAuthn assertion JSON, passed through untouched.</summary>
public sealed record ReAuthPasskeyVerificationBody(
    [property: JsonPropertyName("passkey_id")] string PasskeyId,
    [property: JsonPropertyName("flow_id")] string FlowId,
    [property: JsonPropertyName("response")] JsonElement Response);

public sealed record ReAuthProviderVerificationBody(
    [property: JsonPropertyName("flow_id")] string FlowId,
    [property: JsonPropertyName("credential")] string Credential);

public sealed record ReAuthProviderAuthorizationBody(
    [property: JsonPropertyName("callback_mode")] string CallbackMode,
    [property: JsonPropertyName("completion_proof_challenge")] string? CompletionProofChallenge);

public sealed record ReAuthProviderProofBody([property: JsonPropertyName("flow_id")] string FlowId);

public sealed record RefreshSessionBody(
    [property: JsonPropertyName("dt")] string Dt,
    [property: JsonPropertyName("st")] string St);

public sealed record ReAuthTokenBody([property: JsonPropertyName("re_auth_token")] string ReAuthToken);
