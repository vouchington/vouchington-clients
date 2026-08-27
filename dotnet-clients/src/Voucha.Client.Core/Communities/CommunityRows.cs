using Voucha.Client.Core.Api;
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
    IUiLocalization Localization)
{
  public string PostType => Localization.Resolve(PostTypeText);

  public static CommunityPostRow FromPost(
      Post post,
      PostMetrics? metrics,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(post);
    return new(
        post.Id,
        post.Title ?? post.Slug ?? post.Id,
        post.PostType ?? "post",
        post.CreatedAt,
        metrics?.Count.Descendants ?? 0,
        UiTaxonomy.PostType(post.PostType),
        localization ?? UiLocalization.English);
  }
}
