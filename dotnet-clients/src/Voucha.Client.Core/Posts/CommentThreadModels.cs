using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public static class CommentThreadSorts
{
  public const string Best = "best";
  public const string New = "new";
}

public sealed record CommentThreadNodeViewModel(
    Post Post,
    IReadOnlyList<CommentThreadNodeViewModel> Children)
{
  public string Id => Post.Id;

  public bool HasChildren => Children.Count > 0;
}

public sealed record CommentThreadRow(
    Post Post,
    int Depth,
    bool IsCollapsed,
    bool HasChildren,
    IUiLocalization? Localization = null)
{
  public string Id => Post.Id;

  public string ProtocolPostType => Post.PostType ?? string.Empty;

  public UiText TitleText => string.IsNullOrWhiteSpace(Post.Title)
      ? UiTaxonomy.PostType(ProtocolPostType)
      : UiText.UserContent(Post.Title);

  public string LocalizedTitle => (Localization ?? UiLocalization.English).Resolve(TitleText);

  public string Body => Post.Markdown ?? Post.Html ?? "";

  public string Author => Post.CreatedBy?.Username
      ?? Post.CreatedById
      ?? (Localization ?? UiLocalization.English).Localize(
          UiMessageKey.NativeDotnetPostsAnonymous);

  public string Metadata => Post.LockedAt is null
      ? Author
      : (Localization ?? UiLocalization.English).Format(
          UiMessageKey.NativeDotnetPostsLocked,
          ("author", Author));
}
