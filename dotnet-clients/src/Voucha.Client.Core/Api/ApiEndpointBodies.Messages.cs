using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CreateDirectConversationBody(
    [property: JsonPropertyName("user_ids")] IReadOnlyList<string> UserIds);

public sealed record SendDirectMessageBody(
    [property: JsonPropertyName("text")] string Text);

public sealed record AddDirectConversationParticipantBody(
    [property: JsonPropertyName("user_id")] string UserId);

public sealed record UpdateDirectConversationParticipantPolicyBody(
    [property: JsonPropertyName("participant_add_policy")] string ParticipantAddPolicy);
