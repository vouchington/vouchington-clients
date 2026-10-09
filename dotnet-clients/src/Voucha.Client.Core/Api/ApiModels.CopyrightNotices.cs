using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CopyrightPublicClaimant(
    [property: JsonPropertyName("user_id"), JsonRequired] string UserId,
    [property: JsonPropertyName("display_name"), JsonRequired] string DisplayName);

public abstract record CopyrightNoticeBase
{
  [JsonPropertyName("id")]
  public required string Id { get; init; }
  [JsonPropertyName("jurisdiction")]
  public required string Jurisdiction { get; init; }
  [JsonPropertyName("received_at")]
  public required DateTimeOffset ReceivedAt { get; init; }
  [JsonPropertyName("provisional_withholding_at"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
  public required DateTimeOffset? ProvisionalWithholdingAt { get; init; }
  [JsonPropertyName("target_count")]
  public required int TargetCount { get; init; }
  [JsonPropertyName("claimant"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
  public required CopyrightPublicClaimant? Claimant { get; init; }
}

public record CopyrightNoticeSummary : CopyrightNoticeBase
{
  [JsonPropertyName("accepted_at")]
  public required DateTimeOffset AcceptedAt { get; init; }
}

public sealed record CopyrightNoticeDetail : CopyrightNoticeSummary
{
  [JsonPropertyName("targets")]
  public required IReadOnlyList<CopyrightNoticeTarget> Targets { get; init; }
  [JsonPropertyName("timeline")]
  public required IReadOnlyList<CopyrightNoticeTimelineEvent> Timeline { get; init; }
}

public sealed record CopyrightNoticesResponse(
    [property: JsonPropertyName("copyright_notices")] IReadOnlyList<CopyrightNoticeSummary> CopyrightNotices,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CopyrightNoticeResponse(
    [property: JsonPropertyName("copyright_notice")] CopyrightNoticeDetail CopyrightNotice);

public sealed record CopyrightNoticeTarget(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("hosted_use_url"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] Uri? HostedUseUrl,
    [property: JsonPropertyName("surface")] string Surface,
    [property: JsonPropertyName("restriction_status")] string RestrictionStatus);

public sealed record CopyrightNoticeTimelineEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("event_type")] string EventType,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
