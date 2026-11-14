using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CopyrightStaffQueueResponse(
    [property: JsonPropertyName("copyright_notices")] IReadOnlyList<CopyrightStaffQueueNotice> CopyrightNotices,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CopyrightStaffQueueNotice(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("jurisdiction")] string Jurisdiction,
    [property: JsonPropertyName("received_at")] DateTimeOffset ReceivedAt,
    [property: JsonPropertyName("claimant")] CopyrightStaffClaimant Claimant,
    [property: JsonPropertyName("work_description")] string WorkDescription,
    [property: JsonPropertyName("targets")] IReadOnlyList<CopyrightStaffTarget> Targets,
    [property: JsonPropertyName("evidence")] IReadOnlyList<JsonElement> Evidence,
    [property: JsonPropertyName("form_review")] CopyrightStaffFormReview? FormReview,
    [property: JsonPropertyName("restrictions")] IReadOnlyList<JsonElement> Restrictions,
    [property: JsonPropertyName("appeals")] IReadOnlyList<JsonElement> Appeals,
    [property: JsonPropertyName("counter_notices")] IReadOnlyList<JsonElement> CounterNotices,
    [property: JsonPropertyName("legal_holds")] IReadOnlyList<JsonElement> LegalHolds,
    [property: JsonPropertyName("action_intents")] IReadOnlyList<CopyrightStaffActionIntent> ActionIntents,
    [property: JsonPropertyName("delivery_intents")] IReadOnlyList<JsonElement> DeliveryIntents,
    [property: JsonPropertyName("staydown_matches")] IReadOnlyList<CopyrightStaffStaydownMatch> StaydownMatches,
    [property: JsonPropertyName("staydown_matches_has_more")] bool StaydownMatchesHasMore,
    [property: JsonPropertyName("email_correspondence")] IReadOnlyList<JsonElement> EmailCorrespondence,
    [property: JsonPropertyName("reasons")] IReadOnlyList<string> Reasons,
    [property: JsonPropertyName("waiting_since")] DateTimeOffset WaitingSince,
    [property: JsonPropertyName("next_deadline")] CopyrightStaffDeadline? NextDeadline);

public sealed record CopyrightStaffClaimant(
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("contact")] string Contact,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("misuse")] CopyrightClaimantMisuseSummary? Misuse);

public sealed record CopyrightClaimantMisuseSummary(
    [property: JsonPropertyName("notice_count")] int NoticeCount,
    [property: JsonPropertyName("notice_rejected")] int NoticeRejected,
    [property: JsonPropertyName("notice_withdrawn")] int NoticeWithdrawn,
    [property: JsonPropertyName("restriction_reversed_by_appeal")] int RestrictionReversedByAppeal,
    [property: JsonPropertyName("restriction_reversed_by_counter_notice")] int RestrictionReversedByCounterNotice,
    [property: JsonPropertyName("oldest_notice_at")] DateTimeOffset? OldestNoticeAt,
    [property: JsonPropertyName("newest_notice_at")] DateTimeOffset? NewestNoticeAt,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CopyrightStaffDeadline(
    [property: JsonPropertyName("escalation_at")] DateTimeOffset EscalationAt,
    [property: JsonPropertyName("restoration_deadline_at")] DateTimeOffset RestorationDeadlineAt);

public sealed record CopyrightStaffTarget(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("placement_key")] string PlacementKey,
    [property: JsonPropertyName("placement_revision")] int PlacementRevision,
    [property: JsonPropertyName("image_id")] string ImageId,
    [property: JsonPropertyName("surface")] string Surface,
    [property: JsonPropertyName("provenance")] JsonElement? Provenance,
    [property: JsonPropertyName("hosted_use_url")] Uri HostedUseUrl);

public sealed record CopyrightStaffActionIntent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("target_id"), JsonRequired] string TargetId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("failure_message")] string? FailureMessage);

public sealed record CopyrightStaffStaydownMatch(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("image_id"), JsonRequired] string ImageId,
    [property: JsonPropertyName("registered_image_id")] string RegisteredImageId,
    [property: JsonPropertyName("target_id"), JsonRequired] string TargetId,
    [property: JsonPropertyName("uploaded_by_id")] string UploadedById,
    [property: JsonPropertyName("uploaded_by_username"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? UploadedByUsername,
    [property: JsonPropertyName("image_placement"), JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] CopyrightStaffImagePlacement? ImagePlacement,
    [property: JsonPropertyName("match_kind")] string MatchKind,
    [property: JsonPropertyName("hamming_distance")] int HammingDistance,
    [property: JsonPropertyName("matched_at")] DateTimeOffset MatchedAt);

public sealed record CopyrightStaffImagePlacement(
    [property: JsonPropertyName("image_id"), JsonRequired] string ImageId,
    [property: JsonPropertyName("placement_id"), JsonRequired] string PlacementId,
    [property: JsonPropertyName("placement_revision"), JsonRequired] int PlacementRevision);
