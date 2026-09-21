using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ElectionVoteBody([property: JsonPropertyName("choice")] ElectionVoteChoice Choice);

public sealed record UpdateProfileBody([property: JsonPropertyName("markdown")] string Markdown);

public sealed record ListMutationBody(
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("description")] JsonNullableString? Description = null,
    [property: JsonPropertyName("visibility")] string? Visibility = null);

public sealed record AddListRssFeedItemBody([property: JsonPropertyName("rss_feed_item_id")] string RssFeedItemId);

public sealed record AddListPostBody([property: JsonPropertyName("post_id")] string PostId);

public sealed record ImportCommunityListBody([property: JsonPropertyName("community_slug")] string CommunitySlug);

public sealed record RequestEmailOtpBody(
    [property: JsonPropertyName("emailAddress")] string Email,
    [property: JsonPropertyName("cfTurnstileResponse")] string TurnstileToken,
    [property: JsonPropertyName("ui_locale")] string? UiLocale = null);

public sealed record VerifyEmailOtpBody(
    [property: JsonPropertyName("emailAddress")] string Email,
    [property: JsonPropertyName("token")] string Code);

public sealed record VerifyMfaTotpBody(
    [property: JsonPropertyName("login_attempt_id")] string LoginAttemptId,
    [property: JsonPropertyName("code")] string Code);

/// <summary>
/// WebAuthn credential response; intentionally untyped because the shape varies by platform.
/// </summary>
public sealed record PasskeyAuthVerifyBody([property: JsonPropertyName("response")] object Response);

public sealed record AppleSignInBody(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("nonce")] string? Nonce,
    [property: JsonPropertyName("userData")] AppleSignInUserData? UserData)
{
  public AppleSignInBody(string token, string? nonce, string? userName)
      : this(token, nonce, userName is null ? null : new AppleSignInUserData(userName))
  {
  }
}

public sealed record AppleSignInUserData([property: JsonPropertyName("name")] string Name);

public sealed record SubmitAppealBody(
    [property: JsonPropertyName("target_type")] ModerationAppealTargetType TargetType,
    [property: JsonPropertyName("target_id")] string? TargetId,
    [property: JsonPropertyName("appeal_reason")] string AppealReason,
    [property: JsonPropertyName("post_removal_kind")] ModerationAppealPostRemovalKind? PostRemovalKind,
    [property: JsonPropertyName("cf_turnstile_response")] string? TurnstileToken);

public sealed record UpdateAppealBody(
    [property: JsonPropertyName("public_response")] string? PublicResponse,
    [property: JsonPropertyName("internal_notes")] string? InternalNotes);

public sealed record ResolveAppealBody(
    [property: JsonPropertyName("action")] ModerationAppealAction Action);

public sealed record UpdatePostClearanceBody(
    [property: JsonPropertyName("status")] PostClearanceAction Status,
    [property: JsonPropertyName("reason_code")] string ReasonCode);

public sealed record ResolveReportIntegrityFlagBody(
    [property: JsonPropertyName("resolution")] ReportIntegrityPatchResolution Resolution);

public sealed record ResolveVoteIntegrityFlagBody(
    [property: JsonPropertyName("resolution")] VoteIntegrityResolution Resolution);

public sealed record CreatePostBody(
    [property: JsonPropertyName("post_type")] string PostType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("markdown")] string Markdown,
    [property: JsonPropertyName("cf_turnstile_response")] string? TurnstileToken = null,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("broadcast")] string? Broadcast = null,
    [property: JsonPropertyName("privacy")] string? Privacy = null,
    [property: JsonPropertyName("is_anonymous")] bool IsAnonymous = false,
    [property: JsonPropertyName("url")] Uri? Url = null,
    [property: JsonPropertyName("url_id")] string? LinkIdentifier = null,
    [property: JsonPropertyName("root_id")] string? RootId = null,
    [property: JsonPropertyName("parent_id")] string? ParentId = null,
    [property: JsonPropertyName("review_topic_ratings")] IReadOnlyList<CreatePostReviewTopicRatingInput>? ReviewTopicRatings = null,
    [property: JsonPropertyName("images")] IReadOnlyList<CreatePostImageInput>? Images = null,
    [property: JsonPropertyName("data_point_vertical")] string? DataPointVertical = null,
    [property: JsonPropertyName("structured_data")] object? StructuredData = null,
    [property: JsonPropertyName("declared_language")] string? DeclaredLanguage = null,
    [property: JsonPropertyName("recaptcha_token")] string? RecaptchaToken = null,
    [property: JsonPropertyName("hp_website")] string? HpWebsite = null,
    [property: JsonPropertyName("hp_phone")] string? HpPhone = null,
    [property: JsonPropertyName("categories")] IReadOnlyList<PostCategoryInput>? Categories = null);

public sealed record CreatePostImageInput(
    [property: JsonPropertyName("image_id")] string ImageId,
    [property: JsonPropertyName("order_index")] int OrderIndex,
    [property: JsonPropertyName("caption")] string? Caption = null);

public sealed record CreatePostReviewTopicRatingInput(
    [property: JsonPropertyName("topic_id")] string TopicId,
    [property: JsonPropertyName("rating")] int Rating);

public sealed record CreateReferralLinkBody(
    [property: JsonPropertyName("referral_program_id")] string ReferralProgramId,
    [property: JsonPropertyName("url")] Uri Url,
    [property: JsonPropertyName("label")] string? Label);

public sealed record UpdateReferralLinkBody(
    [property: JsonPropertyName("label")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Label);

public sealed record UpdatePostBody(
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("markdown")] string? Markdown = null,
    [property: JsonPropertyName("broadcast")] string? Broadcast = null,
    [property: JsonPropertyName("privacy")] string? Privacy = null,
    [property: JsonPropertyName("is_anonymous")] bool? IsAnonymous = null,
    [property: JsonPropertyName("data_point_vertical")] string? DataPointVertical = null,
    [property: JsonPropertyName("structured_data")] object? StructuredData = null,
    [property: JsonPropertyName("archive")] bool? Archive = null,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("categories")] IReadOnlyList<PostCategoryInput>? Categories = null);

public sealed record AddPostRatingBody(
    [property: JsonPropertyName("topic_id")] string TopicId,
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("order_index")] int OrderIndex);

public sealed record UpdatePostRatingBody(
    [property: JsonPropertyName("rating")] int? Rating = null,
    [property: JsonPropertyName("order_index")] int? OrderIndex = null);

public sealed record SetPostImagesBody([property: JsonPropertyName("images")] IReadOnlyList<CreatePostImageInput> Images);

public sealed record CreateEntityRelationBody([property: JsonPropertyName("objectId")] string ObjectId);

public sealed record CreateImageUploadUrlBody(
    [property: JsonPropertyName("content_type")] string ContentType,
    [property: JsonPropertyName("content_length")] int ContentLength);

public sealed record CreateSourceBody(
    [property: JsonPropertyName("rss_feed_url")] Uri RssFeedUrl,
    [property: JsonPropertyName("follow")] bool? Follow = null);

public sealed record UpdateRssFeedBody(
    [property: JsonPropertyName("enabled")] bool? Enabled = null,
    [property: JsonPropertyName("discoverable")] bool? Discoverable = null);

public sealed record UpdateTopicBody(
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("markdown")] string? Markdown = null,
    [property: JsonPropertyName("topic_type")] string? TopicType = null,
    [property: JsonPropertyName("noindex")] bool? Noindex = null,
    [property: JsonPropertyName("allow_reviews")] bool? AllowReviews = null,
    [property: JsonPropertyName("hostname")] JsonNullableString? Hostname = null,
    [property: JsonPropertyName("logo_image_id")] JsonNullableString? LogoImageId = null,
    [property: JsonPropertyName("hero_image_id")] JsonNullableString? HeroImageId = null);

public sealed record CreateTopicAliasesBody(
    [property: JsonPropertyName("aliases")] string Aliases);

public sealed record UpdatePodcastPlaybackPositionBody(
    [property: JsonPropertyName("position_seconds")] double PositionSeconds,
    [property: JsonPropertyName("completed")] bool Completed);

public sealed record BeginBlueskyAccountLinkBody([property: JsonPropertyName("handle")] string Handle);
