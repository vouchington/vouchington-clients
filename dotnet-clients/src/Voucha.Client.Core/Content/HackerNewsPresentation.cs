using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Content;

public sealed record HackerNewsLink(string Label, Uri Url);

public sealed class HackerNewsPresentation(HackerNewsReferences? references, IUiLocalization? localization = null)
{
  private IUiLocalization L => localization ?? UiLocalization.English;
  public bool HasReferences => references is { StoryCount: > 0 } or { CommentCount: > 0 };
  public string Seen => L.Format(UiMessageKey.SharedHackerNewsSeenOnCount, ("count", references?.StoryCount ?? 0));
  public string Mentioned => L.Format(UiMessageKey.SharedHackerNewsMentionedOnCount, ("count", references?.CommentCount ?? 0));
  public string MentionedCount => L.Format(UiMessageKey.SharedHackerNewsMentionedCount, ("count", references?.CommentCount ?? 0));
  public string SeenHeading => L.Localize(UiMessageKey.SharedHackerNewsSeenOnHeading);
  public string MentionedHeading => L.Localize(UiMessageKey.SharedHackerNewsMentionedOnHeading);
  public string Summary => string.Join(" · ", new[]
  {
    references?.StoryCount > 0 ? Seen : null,
    references?.CommentCount > 0 ? references.StoryCount > 0 ? MentionedCount : Mentioned : null,
  }.Where(value => value is not null));
  public IReadOnlyList<HackerNewsLink> Stories => references?.Stories?.Select(story => new HackerNewsLink(
      L.Format(UiMessageKey.SharedHackerNewsStorySummary, ("points", story.Points), ("comments", story.ReplyCount)), ItemUrl(story.HackerNewsItemId))).ToArray() ?? [];
  public IReadOnlyList<HackerNewsLink> Comments => references?.Comments?.OrderByDescending(comment => comment.PostedAt).Select(comment => new HackerNewsLink(
      L.Format(UiMessageKey.SharedHackerNewsCommentOn, ("date", comment.PostedAt.ToString("d", L.Culture))), ItemUrl(comment.HackerNewsItemId))).ToArray() ?? [];
  private static Uri ItemUrl(long id) => new($"https://news.ycombinator.com/item?id={id.ToString(CultureInfo.InvariantCulture)}");
}
