using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record CommunityModmailThread(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("channel_type")] string ChannelType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("subject_user_id")] string? SubjectUserId,
    [property: JsonPropertyName("assigned_mod_id")] string? AssignedModId,
    [property: JsonPropertyName("assigned_at")] DateTimeOffset? AssignedAt,
    [property: JsonPropertyName("resolved_at")] DateTimeOffset? ResolvedAt,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record CommunityModmailThreadResponse(
    [property: JsonPropertyName("thread")] CommunityModmailThread Thread);

public sealed record CommunityReportModmailConversation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("channel_type")] string ChannelType);

public sealed record CommunityReportModmailConversationResponse(
    [property: JsonPropertyName("conversation")] CommunityReportModmailConversation Conversation);

public sealed record CommunityModmailMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("body_text")] string BodyText,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("sender_username")] string? SenderUsername,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null);

public sealed record CommunityModmailMessageResponse(
    [property: JsonPropertyName("message")] CommunityModmailMessage Message);

public sealed record CommunityModmailThreadListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<CommunityModmailThread> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CommunityModmailMessageListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<CommunityModmailMessage> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CommunitySavedReply(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("community_id")] string CommunityId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("created_by_id")] string? CreatedById = null,
    [property: JsonPropertyName("order_index")] int? OrderIndex = null,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt = null);

public sealed record CommunitySavedRepliesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<CommunitySavedReply> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CommunitySavedReplyResponse(
    [property: JsonPropertyName("reply")] CommunitySavedReply Reply);

#pragma warning restore CA1054, CA1056, CA1720
