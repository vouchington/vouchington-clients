using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CopyrightParticipantNotice : CopyrightNoticeBase
{
  [JsonPropertyName("accepted_at"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
  public required DateTimeOffset? AcceptedAt { get; init; }
  [JsonPropertyName("targets")]
  public required IReadOnlyList<CopyrightNoticeTarget> Targets { get; init; }
  [JsonPropertyName("timeline")]
  public required IReadOnlyList<CopyrightNoticeTimelineEvent> Timeline { get; init; }
  [JsonPropertyName("statements")]
  public required IReadOnlyList<CopyrightParticipantStatement> Statements { get; init; }
  [JsonPropertyName("viewer_role")]
  public required string ViewerRole { get; init; }
  [JsonPropertyName("respondable_target_ids")]
  public required IReadOnlyList<string> RespondableTargetIds { get; init; }
  [JsonPropertyName("submissions")]
  public required IReadOnlyList<CopyrightParticipantSubmission> Submissions { get; init; }
  [JsonPropertyName("eu")]
  public CopyrightEuParticipantCase? Eu { get; init; }
}

public sealed record CopyrightParticipantNoticeResponse(
    [property: JsonPropertyName("copyright_notice")] CopyrightParticipantNotice CopyrightNotice);

public sealed record CopyrightParticipantStatement(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("delivery_kind")] string DeliveryKind,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("sent_at"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? SentAt,
    [property: JsonPropertyName("text")] string Text);

public sealed record CopyrightParticipantSubmission(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("received_at")] DateTimeOffset ReceivedAt,
    [property: JsonPropertyName("source_kind")] string SourceKind);
