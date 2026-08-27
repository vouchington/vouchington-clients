using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record BeginNativeBlueskyAccountLinkBody(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("completion_proof_challenge")] string CompletionProofChallenge,
    [property: JsonPropertyName("callback_mode")] string CallbackMode = "native");

public sealed record CompleteNativeBlueskyAccountLinkBody(
    [property: JsonPropertyName("flow_id")] string FlowId,
    [property: JsonPropertyName("completion_token")] string CompletionToken,
    [property: JsonPropertyName("completion_proof_verifier")] string CompletionProofVerifier);
