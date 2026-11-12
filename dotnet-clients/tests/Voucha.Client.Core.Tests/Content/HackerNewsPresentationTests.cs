using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Content;

public sealed class HackerNewsPresentationTests
{
  [Fact]
  public void MissingAndEmptyReferencesHaveNoVisibleSummaryOrLinks()
  {
    foreach (var references in new HackerNewsReferences?[] { null, new(0, 0) })
    {
      var presentation = new HackerNewsPresentation(references);
      Assert.False(presentation.HasReferences);
      Assert.Empty(presentation.Summary);
      Assert.Empty(presentation.Stories);
      Assert.Empty(presentation.Comments);
    }
  }

  [Theory]
  [InlineData(0, 2, "Seen on Hacker News · 2")]
  [InlineData(3, 0, "Mentioned on Hacker News · 3")]
  [InlineData(3, 2, "Seen on Hacker News · 2 · Mentioned · 3")]
  public void CompactSummaryDistinguishesStoriesAndMentions(int comments, int stories, string expected)
  {
    var presentation = new HackerNewsPresentation(new(comments, stories));
    Assert.True(presentation.HasReferences);
    Assert.Equal(expected, presentation.Summary);
  }

  [Fact]
  public void DetailsUseStoredItemIdsAndLatestCommentFirst()
  {
    var presentation = new HackerNewsPresentation(new(2, 1,
        [new(302, 301, DateTimeOffset.UnixEpoch), new(303, 301, DateTimeOffset.UnixEpoch.AddDays(1))],
        [new(301, 45, DateTimeOffset.UnixEpoch, 6)]));
    Assert.Equal("Seen on Hacker News", presentation.SeenHeading);
    Assert.Equal("Mentioned on Hacker News", presentation.MentionedHeading);
    Assert.Equal("Mentioned · 2", presentation.MentionedCount);
    Assert.Equal("Seen on Hacker News · 1", presentation.Seen);
    Assert.Equal("Mentioned on Hacker News · 2", presentation.Mentioned);
    var story = Assert.Single(presentation.Stories);
    Assert.Equal("45 points · 6 comments", story.Label);
    Assert.Equal("https://news.ycombinator.com/item?id=301", story.Url.AbsoluteUri);
    Assert.Equal(["https://news.ycombinator.com/item?id=303", "https://news.ycombinator.com/item?id=302"],
        presentation.Comments.Select(comment => comment.Url.AbsoluteUri));
    Assert.Equal("Comment on 1/2/1970", presentation.Comments[0].Label);
  }
}
