using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private const int MaxHashtagLength = 255;

  public void AddDiscussionCategoryTopic()
  {
    var topicId = DiscussionCategoryTopicId.Trim();
    if (topicId.Length == 0) return;

    AddDiscussionCategory(new PostComposeCategoryDraft(PostComposeCategoryKind.Topic, topicId));
    DiscussionCategoryTopicId = "";
  }

  public void AddDiscussionCategoryHashtag()
  {
    var hashtag = TryGetAuthoredHashtag(DiscussionCategoryHashtag);
    if (hashtag is null) return;

    AddDiscussionCategory(new PostComposeCategoryDraft(PostComposeCategoryKind.Hashtag, hashtag));
    DiscussionCategoryHashtag = "";
  }

  public void RemoveDiscussionCategory(PostComposeCategoryDraft category)
  {
    ArgumentNullException.ThrowIfNull(category);
    SetDiscussionCategories(DiscussionCategories.Where(existing => existing != category).ToArray());
  }

  public void SetDiscussionCategories(IReadOnlyList<PostComposeCategoryDraft> categories)
  {
    ArgumentNullException.ThrowIfNull(categories);
    DiscussionCategories = categories;
    OnPropertyChanged(nameof(DiscussionCategories));
  }

  private void AddDiscussionCategory(PostComposeCategoryDraft category)
  {
    if (DiscussionCategories.Contains(category)) return;
    SetDiscussionCategories([.. DiscussionCategories, category]);
  }

  private PostCategoryInput[]? EffectiveDiscussionCategories() =>
      PostType != PostComposeTypes.Discussion || DiscussionCategories.Count == 0
          ? null
          : DiscussionCategories.Select(ToPostCategoryInput).ToArray();

  private static PostCategoryInput ToPostCategoryInput(PostComposeCategoryDraft category) => category.Kind switch
  {
    PostComposeCategoryKind.Topic => new TopicPostCategoryInput(category.Value),
    PostComposeCategoryKind.Hashtag => new HashtagPostCategoryInput(category.Value),
    _ => throw new ArgumentOutOfRangeException(nameof(category)),
  };

  [SuppressMessage(
      "Globalization",
      "CA1308:Normalize strings to uppercase",
      Justification = "The API contract canonicalizes hashtag slugs as lowercase ASCII.")]
  private static string? TryGetAuthoredHashtag(string input)
  {
    var authored = input.Trim();
    if (authored.Length is 0 or > MaxHashtagLength) return null;
    var canonical = authored.StartsWith('#') ? authored[1..] : authored;
    canonical = RepeatedHyphens().Replace(canonical.Replace('.', '-').Replace('_', '-'), "-")
        .ToLowerInvariant();
    return canonical.Length is > 0 and <= MaxHashtagLength && HashtagPattern().IsMatch(canonical)
        ? authored
        : null;
  }

  [GeneratedRegex("-+")]
  private static partial Regex RepeatedHyphens();

  [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
  private static partial Regex HashtagPattern();
}
