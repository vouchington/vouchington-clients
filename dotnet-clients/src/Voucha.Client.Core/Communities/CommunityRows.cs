using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed record CommunityMemberRow(
    string Id,
    string UserId,
    string DisplayName,
    string ProtocolRole,
    DateTimeOffset? CreatedAt,
    UiText RoleText,
    IUiLocalization Localization)
{
  public string Role => Localization.Resolve(RoleText);

  public static CommunityMemberRow FromMember(
      CommunityMember member,
      PublicUser? user,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(member);
    return new(
        member.Id,
        member.UserId,
        user?.Name ?? user?.Username ?? member.UserId,
        member.Role,
        member.CreatedAt,
        UiTaxonomy.CommunityMemberRole(member.Role),
        localization ?? UiLocalization.English);
  }
}

public sealed record CommunityPostRow(
    string Id,
    string Title,
    string ProtocolPostType,
    DateTimeOffset? CreatedAt,
    int ReplyCount,
    UiText PostTypeText,
    IUiLocalization Localization,
    UrlEmbedPreview? EmbedPreview = null,
    string? DeclaredLanguage = null,
    string? DetectedLanguage = null,
    bool HasAuthoredTitle = false)
{
  public string PostType => Localization.Resolve(PostTypeText);

  public AuthoredContentLanguage ContentLanguage => AuthoredContentLanguage.Resolve(DeclaredLanguage, DetectedLanguage);
  public string? TitleFlowDirection => HasAuthoredTitle ? ContentLanguage.Direction?.ToString() : null;

  public static CommunityPostRow FromPost(
      Post post,
      PostMetrics? metrics,
      IUiLocalization? localization) =>
      FromPost(post, metrics, embed: null, localization);

  public static CommunityPostRow FromPost(
      Post post,
      PostMetrics? metrics,
      UrlEmbed? embed = null,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(post);
    return new(
        post.Id,
        Normalize(post.Title) ?? Normalize(post.Slug) ?? post.Id,
        post.PostType ?? "post",
        post.CreatedAt,
        metrics?.Count.Descendants ?? 0,
        UiTaxonomy.PostType(post.PostType),
        localization ?? UiLocalization.English,
        UrlEmbedPreviews.From(embed), Normalize(post.Title) is null ? null : post.DeclaredLanguage,
        Normalize(post.Title) is null ? null : post.LinguaRsDetectedLanguage,
        HasAuthoredTitle: Normalize(post.Title) is not null);
  }

  private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
