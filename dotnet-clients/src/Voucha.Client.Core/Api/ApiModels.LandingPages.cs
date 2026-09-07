using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record LandingPage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("subtitle")] string? Subtitle,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("is_default")] bool IsDefault,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("items")] IReadOnlyList<LandingPageItem>? Items = null);

public sealed record LandingPagesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<LandingPage> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record LandingPageDetailResponse(
    [property: JsonPropertyName("landing_page")] LandingPage LandingPage);

public sealed record LandingPageResponse(
    [property: JsonPropertyName("landing_page")] LandingPage LandingPage);

public sealed record LandingPageCandidatesResponse(
    [property: JsonPropertyName("candidates")] LandingPageCandidates Candidates);

public sealed record LandingPageCandidates(
    [property: JsonPropertyName("can_create_landing_pages")] bool CanCreateLandingPages,
    [property: JsonPropertyName("profile_links")] IReadOnlyList<LandingPageProfileLink> ProfileLinks,
    [property: JsonPropertyName("reviews")] IReadOnlyList<LandingPageReview> Reviews,
    [property: JsonPropertyName("referral_links")] IReadOnlyList<LandingPageReferralLink> ReferralLinks);

public sealed record LandingPageProfileLink(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("link_type")] string LinkType,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("url_id")] string? LinkReferenceId,
    [property: JsonPropertyName("url")] Uri? Url,
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("image_id")] string? ImageId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record LandingPageReviewTopicRating(
    [property: JsonPropertyName("topic_id")] string TopicId,
    [property: JsonPropertyName("topic_name")] string TopicName,
    [property: JsonPropertyName("topic_slug")] string TopicSlug,
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("order_index")] int OrderIndex);

public sealed record LandingPageReview(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("markdown")] string Markdown,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("review_topic_ratings")] IReadOnlyList<LandingPageReviewTopicRating> ReviewTopicRatings,
    [property: JsonPropertyName("declared_language")] string? DeclaredLanguage = null,
    [property: JsonPropertyName("lingua_rs_detected_language")] string? LinguaRsDetectedLanguage = null);

public sealed record LandingPageReferralLink(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("referral_program_id")] string ReferralProgramId,
    [property: JsonPropertyName("referral_program_name")] string ReferralProgramName,
    [property: JsonPropertyName("referral_program_slug")] string ReferralProgramSlug,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("url")] Uri Url);

public sealed record LandingPageTopic(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("topic_type")] string TopicType);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LandingPageProfileLinkItem), "profile_link")]
[JsonDerivedType(typeof(LandingPageReviewItem), "review")]
[JsonDerivedType(typeof(LandingPageReferralLinkItem), "referral_link")]
[JsonDerivedType(typeof(LandingPageTopicGroupItem), "topic_group")]
[JsonDerivedType(typeof(LandingPageLinkItem), "link")]
public abstract record LandingPageItem(
    [property: JsonPropertyName("id")] string Id)
{
  [JsonIgnore]
  public abstract string Type { get; }
}

public sealed record LandingPageProfileLinkItem(
    string Id,
    [property: JsonPropertyName("profile_link")] LandingPageProfileLink ProfileLink)
    : LandingPageItem(Id)
{
  [JsonIgnore]
  public override string Type => "profile_link";
}

public sealed record LandingPageReviewItem(
    string Id,
    [property: JsonPropertyName("review")] LandingPageReview Review)
    : LandingPageItem(Id)
{
  [JsonIgnore]
  public override string Type => "review";
}

public sealed record LandingPageReferralLinkItem(
    string Id,
    [property: JsonPropertyName("referral_link")] LandingPageReferralLink ReferralLink)
    : LandingPageItem(Id)
{
  [JsonIgnore]
  public override string Type => "referral_link";
}

public sealed record LandingPageTopicGroupItem(
    string Id,
    [property: JsonPropertyName("topic")] LandingPageTopic Topic,
    [property: JsonPropertyName("entries")] IReadOnlyList<LandingPageItem> Entries)
    : LandingPageItem(Id)
{
  [JsonIgnore]
  public override string Type => "topic_group";
}

public sealed record LandingPageLinkItem(
    string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("url")] Uri Url)
    : LandingPageItem(Id)
{
  [JsonIgnore]
  public override string Type => "link";
}
