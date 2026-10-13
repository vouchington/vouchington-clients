using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostContentPresentationTests
{
  [Theory]
  [InlineData("review")]
  [InlineData("discussion")]
  [InlineData("data_point")]
  [InlineData("comment")]
  [InlineData("article")]
  [InlineData("blog_post")]
  [InlineData("story")]
  [InlineData("link")]
  [InlineData("topic_recommendation")]
  public void RejectedAuthoredRowsShowOnlyViewerPermittedContent(string postType)
  {
    var post = new Post("post-1", postType, "Title", "Sensitive body", "author-1",
        ClearanceStatus: "rejected", ClearanceReason: "off_topic");
    var row = PostRows.From(post, elections: null, votes: null, bookmarks: null);
    var author = row with { ViewerId = "author-1" };
    var other = row with { ViewerId = "other" };
    var staff = row with { ViewerId = "other", IsStaffViewer = true };

    Assert.False(author.ShowBodyContent);
    Assert.True(author.ShowAppealsAction);
    Assert.Equal("This content was removed by a moderator.", author.LocalizedNotice);
    Assert.DoesNotContain("off_topic", author.LocalizedNotice);
    Assert.Equal("off_topic", author.LocalizedRemovalReason);
    Assert.False(other.ShowBodyContent);
    Assert.False(other.ShowAppealsAction);
    Assert.Equal("This content is unavailable.", other.LocalizedNotice);
    Assert.True(staff.ShowBodyContent);
    Assert.Equal("in review", staff.LocalizedReviewBadge);
  }

  [Theory]
  [InlineData("pending")]
  [InlineData("in_review")]
  [InlineData("approved")]
  public void NonrejectedClearanceDoesNotClaimPublicVisibilityOrRetainRemovalNotice(string clearance)
  {
    var row = PostRows.From(new Post("post-1", "discussion", "Title", "Body", "author-1",
        ClearanceStatus: clearance), elections: null, votes: null, bookmarks: null) with
    {
      ViewerId = "author-1",
    };

    Assert.True(row.ShowBodyContent);
    Assert.False(row.ShowNotice);
    Assert.False(row.ShowAppealsAction);
    Assert.Null(row.LocalizedNotice);
    Assert.Null(row.LocalizedRemovalReason);
  }

  [Fact]
  public void RejectedAuthorWithoutPublicReasonGetsLocalizedFallback()
  {
    var row = PostRows.From(new Post("post-1", "discussion", "Title", "Body", "author-1",
        ClearanceStatus: "rejected"), elections: null, votes: null, bookmarks: null) with
    {
      ViewerId = "author-1",
    };

    Assert.Equal("No additional reason was provided.", row.LocalizedRemovalReason);
  }

  [Fact]
  public void RejectedStaffAuthorCanSeeMediaButStillGetsAuthorNotice()
  {
    var embed = new UrlEmbedPreview("A safe preview", null, null, null,
        new Uri("https://example.com/source"), null);
    var row = PostRows.From(new Post("post-1", "link", "Title", "Private body", "staff-1",
        ClearanceStatus: "rejected"), elections: null, votes: null, bookmarks: null) with
    {
      ViewerId = "staff-1",
      IsStaffViewer = true,
      EmbedPreview = embed,
    };

    Assert.Equal(PostContentPresentation.RemovedForAuthor, row.ContentPresentation);
    Assert.False(row.ShowBodyContent);
    Assert.True(row.ShowAppealsAction);
    Assert.True(row.ShowEmbed);
    Assert.False((row with { IsStaffViewer = false }).ShowEmbed);
  }

  [Fact]
  public void LateRejectionAndRecoveryReprojectTheSamePostForEachViewer()
  {
    var post = new Post("post-1", "review", "Title", "Body", "author-1",
        ClearanceStatus: "approved");
    foreach (var (status, authorBody, otherBody) in new[]
    {
      ("approved", true, true),
      ("rejected", false, false),
      ("in_review", true, true),
      ("rejected", false, false),
      ("approved", true, true),
    })
    {
      var row = PostRows.From(post with { ClearanceStatus = status },
          elections: null, votes: null, bookmarks: null);
      var author = row with { ViewerId = "author-1" };
      var other = row with { ViewerId = "other" };

      Assert.Equal("post-1", row.Id);
      Assert.Equal(authorBody, author.ShowBodyContent);
      Assert.Equal(otherBody, other.ShowBodyContent);
      Assert.Equal(!authorBody, author.ShowAppealsAction);
      Assert.Equal(!otherBody, other.ShowNotice);
      Assert.Equal(!authorBody, author.ShowNotice);
      Assert.Equal(!otherBody, other.LocalizedNotice == "This content is unavailable.");
    }
  }
}
