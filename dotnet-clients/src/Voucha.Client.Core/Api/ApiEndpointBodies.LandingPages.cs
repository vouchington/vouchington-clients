using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CreateLandingPageBody(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("subtitle")] JsonNullableString? Subtitle = null,
    [property: JsonPropertyName("slug")] string? Slug = null);

public sealed record UpdateLandingPageBody(
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("subtitle")] JsonNullableString? Subtitle = null,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("is_default")] bool? IsDefault = null);

public sealed record SetLandingPageDefaultBody(
    [property: JsonPropertyName("is_default")] bool IsDefault = true);

public sealed record ReplaceLandingPageItemsBody(
    [property: JsonPropertyName("items")] IReadOnlyList<LandingPageItemInput> Items);

public record LandingPageItemInput(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("profile_link_id")] string? ProfileLinkId = null,
    [property: JsonPropertyName("review_id")] string? ReviewId = null,
    [property: JsonPropertyName("referral_link_id")] string? ReferralLinkId = null,
    [property: JsonPropertyName("topic_id")] string? TopicId = null,
    [property: JsonPropertyName("entries")] IReadOnlyList<LandingPageItemInput>? Entries = null,
    [property: JsonPropertyName("label")] string? Label = null,
    [property: JsonPropertyName("url")] Uri? Url = null);

public sealed record LandingPageProfileLinkItemInput(string ProfileLinkId)
    : LandingPageItemInput("profile_link", ProfileLinkId: ProfileLinkId);

public sealed record LandingPageReviewItemInput(string ReviewId)
    : LandingPageItemInput("review", ReviewId: ReviewId);

public sealed record LandingPageReferralLinkItemInput(string ReferralLinkId)
    : LandingPageItemInput("referral_link", ReferralLinkId: ReferralLinkId);

public sealed record LandingPageTopicGroupItemInput(
    string TopicId,
    IReadOnlyList<LandingPageItemInput> Entries)
    : LandingPageItemInput("topic_group", TopicId: TopicId, Entries: Entries);

public sealed record LandingPageLinkItemInput(string Label, Uri Url)
    : LandingPageItemInput("link", Label: Label, Url: Url);
