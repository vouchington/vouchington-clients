using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record GrantIdentityVerificationAttemptBody(
    [property: JsonPropertyName("note")] string Note);

public sealed record GrantIdentityVerificationAttemptResponse(
    [property: JsonPropertyName("granted")] bool Granted);
