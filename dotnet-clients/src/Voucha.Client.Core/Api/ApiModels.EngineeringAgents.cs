using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record AgentSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("system_user_id")] string SystemUserId,
    [property: JsonPropertyName("agent_type")] string AgentType,
    [property: JsonPropertyName("activated_at")] DateTimeOffset? ActivatedAt,
    [property: JsonPropertyName("deactivated_at")] DateTimeOffset? DeactivatedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("moderator")] AgentModerator? Moderator = null);

public sealed record AgentModerator(
    [property: JsonPropertyName("agent_id")] string AgentId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record AgentDetailResponse(
    [property: JsonPropertyName("agent")] AgentSummary Agent,
    [property: JsonPropertyName("user")] PublicUser? User);

public enum AgentConversationFilterKind { UserId, Username, PostId, PostSlug, RssFeedItemId }

public sealed record AgentConversationFilter(AgentConversationFilterKind Kind, string Value)
{
  public string QueryName => QueryNameFor(Kind);

  public static AgentConversationFilter? Create(AgentConversationFilterKind kind, string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : new(kind, value.Trim());

  public static AgentConversationFilter? FromQuery(IReadOnlyDictionary<string, string> queryItems)
  {
    ArgumentNullException.ThrowIfNull(queryItems);
    foreach (var kind in new[]
    {
      AgentConversationFilterKind.UserId,
      AgentConversationFilterKind.Username,
      AgentConversationFilterKind.PostId,
      AgentConversationFilterKind.PostSlug,
      AgentConversationFilterKind.RssFeedItemId,
    })
    {
      if (Create(kind, queryItems.GetValueOrDefault(QueryNameFor(kind))) is { } filter) return filter;
    }
    return null;
  }

  private static string QueryNameFor(AgentConversationFilterKind kind) => kind switch
  {
    AgentConversationFilterKind.UserId => "user_id",
    AgentConversationFilterKind.Username => "username",
    AgentConversationFilterKind.PostId => "post_id",
    AgentConversationFilterKind.PostSlug => "post_slug",
    _ => "rss_feed_item_id",
  };
}

public sealed record AgentsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<AgentSummary> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, PublicUser> Users);

public sealed record AgentConversationSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("updated_by_id")] string? UpdatedById,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt,
    [property: JsonPropertyName("deleted_by_id")] string? DeletedById);

public sealed record AgentConversationsResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<AgentConversationSummary> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, PublicUser> Users);

public sealed record AgentConversation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("channel_type")] string ChannelType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("updated_by_id")] string? UpdatedById,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt,
    [property: JsonPropertyName("deleted_by_id")] string? DeletedById,
    [property: JsonPropertyName("last_response_id")] string? LastResponseId);

public sealed record AgentConversationMessageContent(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("error")] string? Error = null);

public sealed record AgentConversationMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("updated_by_id")] string? UpdatedById,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt,
    [property: JsonPropertyName("deleted_by_id")] string? DeletedById,
    [property: JsonPropertyName("content")] AgentConversationMessageContent? Content)
{ }

public sealed record AgentConversationDetailResponse(
    [property: JsonPropertyName("conversation")] AgentConversation Conversation,
    [property: JsonPropertyName("results")] IReadOnlyList<AgentConversationMessage> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record FetchAgentConversationRequest(
    string AgentIdOrSlug,
    string ConversationId,
    string? After = null,
    int Limit = 50);

public sealed record FetchAgentsRequest(string? After = null, int Limit = 25);

public sealed record FetchAgentConversationsRequest(
    string AgentIdOrSlug, string? After = null, int Limit = 25, AgentConversationFilter? Filter = null);
