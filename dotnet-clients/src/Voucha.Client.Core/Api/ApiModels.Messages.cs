using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record DirectConversation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("channel_type")] string ChannelType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("created_by_id")] string? CreatedById = null,
    [property: JsonPropertyName("participant_usernames")] IReadOnlyList<string>? ParticipantUsernames = null,
    [property: JsonPropertyName("participant_add_policy")] string? ParticipantAddPolicy = null,
    [property: JsonPropertyName("participants")] IReadOnlyList<DirectMessageParticipant>? Participants = null);

public sealed record DirectConversationsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<DirectConversation> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record DirectConversationResponse(
    [property: JsonPropertyName("conversation")] DirectConversation Conversation);

public sealed record DirectMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("body_text")] string BodyText,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("sender_username")] string? SenderUsername = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt = null);

public sealed record DirectMessagesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<DirectMessage> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record DirectMessageResponse(
    [property: JsonPropertyName("message")] DirectMessage Message);

public sealed record DirectMessageParticipant(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("removed_at")] DateTimeOffset? RemovedAt = null,
    [property: JsonPropertyName("username")] string? Username = null,
    [property: JsonPropertyName("profile_image_id")] string? ProfileImageId = null);

public sealed record DirectMessageParticipantsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<DirectMessageParticipant> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record DirectMessageParticipantResponse(
    [property: JsonPropertyName("participant")] DirectMessageParticipant Participant);

public sealed record DirectConversationPolicyResponse(
    [property: JsonPropertyName("participant_add_policy")] string ParticipantAddPolicy);

public sealed record UsersSearchResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<UserSearchResult> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record FetchDirectMessagesRequest(
    string? After = null,
    int Limit = 20);

public sealed record FetchDirectConversationMessagesRequest(
    string ConversationId,
    string? After = null,
    int Limit = 20);

public sealed record CreateDirectConversationRequest(
    IReadOnlyList<string> UserIds);

public sealed record SendDirectMessageRequest(
    string ConversationId,
    string Text);

public sealed record AddDirectConversationParticipantRequest(
    string ConversationId,
    string UserId);

public sealed record UpdateDirectConversationParticipantPolicyRequest(
    string ConversationId,
    string ParticipantAddPolicy);

public sealed record SearchUsersRequest(
    string Query,
    string? After = null,
    int Limit = 10);

#pragma warning restore CA1054, CA1056, CA1720
