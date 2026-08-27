using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record BeginOAuthAuthorizationBody(
    [property: JsonPropertyName("purpose")] OAuthAuthorizationPurpose Purpose,
    [property: JsonPropertyName("callback_mode")] string CallbackMode,
    [property: JsonPropertyName("completion_proof_challenge")] string CompletionProofChallenge);

public sealed record CompleteOAuthAuthorizationBody(
    [property: JsonPropertyName("completion_token")] string CompletionToken,
    [property: JsonPropertyName("completion_proof_verifier")] string CompletionProofVerifier);
