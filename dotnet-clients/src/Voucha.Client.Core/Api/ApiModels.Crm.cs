using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum CrmContactType
{
  Influencer,
  Customer,
  Partner,
}

public enum CrmContactSource
{
  CsvImport,
  Manual,
  InboundEmail,
  Referral,
}

public enum CrmContactVertical
{
  CreditCards,
  Travel,
  Cars,
  Ai,
  Technology,
  Finance,
  Lifestyle,
  Other,
}

public enum CrmSocialPlatform
{
  Instagram,
  Tiktok,
  Youtube,
  X,
  Linkedin,
}

public enum CrmMessageDirection
{
  Inbound,
  Outbound,
}

public enum CrmEmailProvider
{
  Ses,
  GmailSmtp,
}

public enum CrmContactStatus
{
  New,
  AwaitingResponse,
  InConversation,
  Converted,
  Archived,
  OptedOut,
}

public sealed record CrmContact(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("vertical")] CrmContactVertical? Vertical,
    [property: JsonPropertyName("contact_type")] CrmContactType ContactType,
    [property: JsonPropertyName("source")] CrmContactSource Source,
    [property: JsonPropertyName("follower_count")] int? FollowerCount,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("metadata")] IReadOnlyDictionary<string, object>? Metadata,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("assigned_to_id")] string? AssignedToId,
    [property: JsonPropertyName("created_by_id")] string CreatedById,
    [property: JsonPropertyName("contacted_at")] DateTimeOffset? ContactedAt,
    [property: JsonPropertyName("responded_at")] DateTimeOffset? RespondedAt,
    [property: JsonPropertyName("converted_at")] DateTimeOffset? ConvertedAt,
    [property: JsonPropertyName("opted_out_at")] DateTimeOffset? OptedOutAt,
    [property: JsonPropertyName("archived_at")] DateTimeOffset? ArchivedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CrmContactSocialAccount(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("contact_id")] string ContactId,
    [property: JsonPropertyName("platform")] CrmSocialPlatform Platform,
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("profile_url")] Uri? ProfileUrl,
    [property: JsonPropertyName("follower_count")] int? FollowerCount,
    [property: JsonPropertyName("follower_count_updated_at")] DateTimeOffset? FollowerCountUpdatedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CrmContactListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<CrmContact> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CrmContactDetailResponse(
    [property: JsonPropertyName("contact")] CrmContact Contact,
    [property: JsonPropertyName("social_accounts")] IReadOnlyList<CrmContactSocialAccount> SocialAccounts);

public sealed record CrmContactResponse(
    [property: JsonPropertyName("contact")] CrmContact Contact);

public sealed record CrmMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("direction")] CrmMessageDirection Direction,
    [property: JsonPropertyName("from_email")] string FromEmail,
    [property: JsonPropertyName("to_email")] string ToEmail,
    [property: JsonPropertyName("subject")] string? Subject,
    [property: JsonPropertyName("body_text")] string? BodyText,
    [property: JsonPropertyName("body_html")] string? BodyHtml,
    [property: JsonPropertyName("email_provider")] CrmEmailProvider? EmailProvider,
    [property: JsonPropertyName("ses_message_id")] string? SesMessageId,
    [property: JsonPropertyName("sent_at")] DateTimeOffset? SentAt,
    [property: JsonPropertyName("delivered_at")] DateTimeOffset? DeliveredAt,
    [property: JsonPropertyName("bounced_at")] DateTimeOffset? BouncedAt,
    [property: JsonPropertyName("received_at")] DateTimeOffset? ReceivedAt,
    [property: JsonPropertyName("discarded_at")] DateTimeOffset? DiscardedAt,
    [property: JsonPropertyName("ai_prompt")] string? AiPrompt,
    [property: JsonPropertyName("ai_generated_at")] DateTimeOffset? AiGeneratedAt,
    [property: JsonPropertyName("sent_by_id")] string? SentById,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CrmEmailListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<CrmMessage> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CrmMessageResponse(
    [property: JsonPropertyName("message")] CrmMessage Message);

public sealed record CrmNote(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("contact_id")] string ContactId,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("created_by_id")] string CreatedById,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("__entity_type")] string? EntityType = null);

public sealed record CrmNoteListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<CrmNote> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CrmNoteResponse(
    [property: JsonPropertyName("note")] CrmNote Note);

public sealed record CrmEmailDraft(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("body_html")] string BodyHtml,
    [property: JsonPropertyName("body_text")] string BodyText);

public sealed record CrmEmailDraftResponse(
    [property: JsonPropertyName("draft")] CrmEmailDraft Draft);
