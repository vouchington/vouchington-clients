using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public static class CommentPlaceholder
{
  public static bool IsPlaceholder(Post post)
  {
    ArgumentNullException.ThrowIfNull(post);
    return !string.IsNullOrEmpty(post.PlaceholderState);
  }

  public static UiText Label(Post post)
  {
    ArgumentNullException.ThrowIfNull(post);
    return UiText.Localized(post.PlaceholderState == "deleted"
        ? UiMessageKey.NativeDotnetPostsDeleted
        : UiMessageKey.NativeDotnetModerationPending);
  }
}
