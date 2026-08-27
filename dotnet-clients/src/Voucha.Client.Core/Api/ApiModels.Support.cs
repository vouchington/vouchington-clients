using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum SupportThreadStatus { Open, Assigned, Resolved, Closed }

public enum StaffSupportThreadStatusFilter { Open, Assigned, Resolved }

public static class SupportThreadStatusExtensions
{
  public static string ToWireValue(this SupportThreadStatus status) => status switch
  {
    SupportThreadStatus.Open => "open",
    SupportThreadStatus.Assigned => "assigned",
    SupportThreadStatus.Resolved => "resolved",
    SupportThreadStatus.Closed => "closed",
    _ => throw new ArgumentOutOfRangeException(nameof(status)),
  };

  public static string ToWireValue(this StaffSupportThreadStatusFilter status) => status switch
  {
    StaffSupportThreadStatusFilter.Open => "open",
    StaffSupportThreadStatusFilter.Assigned => "assigned",
    StaffSupportThreadStatusFilter.Resolved => "resolved",
    _ => throw new ArgumentOutOfRangeException(nameof(status)),
  };
}

public sealed record SupportThread(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("support_contact_id")] string SupportContactId,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("conversation_id")] string? ConversationId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("assigned_at")] DateTimeOffset? AssignedAt,
    [property: JsonPropertyName("assigned_to_id")] string? AssignedToId,
    [property: JsonPropertyName("resolved_at")] DateTimeOffset? ResolvedAt,
    [property: JsonPropertyName("resolved_by_id")] string? ResolvedById,
    [property: JsonPropertyName("status")] SupportThreadStatus Status = SupportThreadStatus.Open,
    [property: JsonPropertyName("contact_user_id")] string? ContactUserId = null);

public sealed record SupportMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("support_thread_id")] string SupportThreadId,
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("body_text")] string BodyText,
    [property: JsonPropertyName("body_html")] string BodyHtml,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("created_by_id")] string? CreatedById,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("email_message_id")] string? EmailMessageId,
    [property: JsonPropertyName("email_subject")] string? EmailSubject,
    [property: JsonPropertyName("email_from")] string? EmailFrom,
    [property: JsonPropertyName("email_to")] string? EmailTo,
    [property: JsonPropertyName("drafted_at")] DateTimeOffset? DraftedAt,
    [property: JsonPropertyName("edited_at")] DateTimeOffset? EditedAt,
    [property: JsonPropertyName("edited_by_id")] string? EditedById,
    [property: JsonPropertyName("approved_at")] DateTimeOffset? ApprovedAt,
    [property: JsonPropertyName("approved_by_id")] string? ApprovedById,
    [property: JsonPropertyName("sent_at")] DateTimeOffset? SentAt);

public sealed record SupportThreadListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<SupportThread> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
public sealed record SupportThreadDetailResponse(
    [property: JsonPropertyName("thread")] SupportThread Thread,
    [property: JsonPropertyName("messages")] IReadOnlyList<SupportMessage> Messages,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
public sealed record SupportThreadResponse([property: JsonPropertyName("thread")] SupportThread Thread);
public sealed record SupportMessageListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<SupportMessage> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
public sealed record SupportMessageResponse([property: JsonPropertyName("message")] SupportMessage Message);
public sealed record SupportDraftQueuedResponse([property: JsonPropertyName("queued")] bool Queued);

public sealed record CreateSupportThreadBody(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("message")] string? Message = null,
    [property: JsonPropertyName("conversation_id")] string? ConversationId = null);
public sealed record CreateSupportThreadResponse(
    [property: JsonPropertyName("thread")] SupportThread Thread,
    [property: JsonPropertyName("message")] SupportMessage? Message = null);
public sealed record AssignSupportThreadBody([property: JsonPropertyName("assigned_to_id")] string AssignedToId);
public sealed record ResolveSupportThreadBody([property: JsonPropertyName("resolved")] bool Resolved);
public sealed record SupportMessageBody([property: JsonPropertyName("body_text")] string BodyText);

public sealed record SupportContact(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email_address")] string EmailAddress,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);
public sealed record SupportContactListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<SupportContact> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);
public sealed record SupportContactDetailResponse(
    [property: JsonPropertyName("contact")] SupportContact Contact,
    [property: JsonPropertyName("threads")] IReadOnlyList<SupportThread> Threads,
    [property: JsonPropertyName("thread_page_info")] PageInfo ThreadPageInfo);
public sealed record SupportContactResponse([property: JsonPropertyName("contact")] SupportContact Contact);
public sealed record UpdateSupportContactBody(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("notes")] string? Notes);
