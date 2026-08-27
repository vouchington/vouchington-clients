using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostComposeValidationTests
{
  private const string TopicId = "01234567-89ab-7def-0123-456789abcdef";
  private const string OtherTopicId = "01234567-89ab-7def-0123-456789abcde0";

  [Theory]
  [InlineData("Short review.")]
  [InlineData("This review has enough characters to clear that gate, but it still has too few words.")]
  [InlineData("This review has enough characters and enough words to pass two of the checks but not the sentence count because it keeps going without the third clear sentence")]
  public void NonAdminReviewsMustMeetBackendContentMinimums(string markdown)
  {
    var viewModel = NewViewModel();
    viewModel.PostType = PostComposeTypes.Review;
    viewModel.Title = "Review";
    viewModel.Markdown = markdown;
    viewModel.ReviewTopicId = TopicId;
    viewModel.ReviewRating = "5";

    Assert.Equal(
        "Review markdown must be at least 150 characters, 30 words, and 3 sentences.",
        viewModel.Validation.Message);
  }

  [Fact]
  public void AvailablePostTypesExcludeStoryAndCollapseForCommunityCompose()
  {
    var viewModel = NewViewModel();

    Assert.DoesNotContain("story", viewModel.AvailablePostTypes);
    Assert.Equal(
        [PostComposeTypes.Discussion, PostComposeTypes.Review, PostComposeTypes.DataPoint, PostComposeTypes.Link],
        viewModel.AvailablePostTypes);

    viewModel.CommunitySlug = "travel";

    Assert.Equal(
        [PostComposeTypes.Discussion, PostComposeTypes.Review, PostComposeTypes.DataPoint],
        viewModel.AvailablePostTypes);
  }

  [Fact]
  public void CommunityComposeResetsUnsupportedCurrentType()
  {
    var viewModel = NewViewModel(new StubSessionStore(["administrator"]));
    viewModel.PostType = PostComposeTypes.Article;

    viewModel.CommunitySlug = "travel";

    Assert.Equal(PostComposeTypes.Discussion, viewModel.PostType);
  }

  [Fact]
  public void AdminReviewsCanBypassReviewContentMinimums()
  {
    var viewModel = NewViewModel(new StubSessionStore(["administrator"]));
    viewModel.PostType = PostComposeTypes.Review;
    viewModel.Title = "Review";
    viewModel.Markdown = "Short review.";
    viewModel.ReviewTopicId = TopicId;
    viewModel.ReviewRating = "5";

    Assert.True(viewModel.Validation.CanPublish);
  }

  [Theory]
  [InlineData("not-a-uuid", 5)]
  [InlineData(TopicId, 0)]
  [InlineData(TopicId, 6)]
  public void ReviewTopicRatingsMustUseUuidTopicsAndOneThroughFiveRatings(string topicId, int rating)
  {
    var viewModel = NewViewModel();
    viewModel.PostType = PostComposeTypes.Review;
    viewModel.Title = "Review";
    viewModel.Markdown = ValidReviewMarkdown;
    viewModel.SetReviewTopicRatings([new PostComposeTopicRatingDraft(topicId, rating)]);

    Assert.Equal(
        "Review topic ratings must use UUID topic IDs and ratings from 1 to 5.",
        viewModel.Validation.Message);
  }

  [Fact]
  public void StoryPostTypeIsRejected()
  {
    var viewModel = NewViewModel();
    viewModel.Title = "Story";
    viewModel.Markdown = "Story body";
    viewModel.PostType = "story";

    Assert.Equal("Choose a supported post type.", viewModel.Validation.Message);
  }

  [Fact]
  public void ReviewTopicRatingsRejectDuplicateTopicsAndAllEqualMultiRatings()
  {
    var viewModel = NewViewModel();
    viewModel.PostType = PostComposeTypes.Review;
    viewModel.Title = "Review";
    viewModel.Markdown = ValidReviewMarkdown;
    viewModel.SetReviewTopicRatings([
        new PostComposeTopicRatingDraft(TopicId, 5),
        new PostComposeTopicRatingDraft(TopicId, 4)]);

    Assert.Equal(
        "Review topic ratings must use UUID topic IDs and ratings from 1 to 5.",
        viewModel.Validation.Message);

    viewModel.SetReviewTopicRatings([
        new PostComposeTopicRatingDraft(TopicId, 5),
        new PostComposeTopicRatingDraft(OtherTopicId, 5)]);

    Assert.Equal(
        "Review topic ratings must use UUID topic IDs and ratings from 1 to 5.",
        viewModel.Validation.Message);
  }

  [Fact]
  public void DataPointStructuredDataMustMatchCreditCardSchema()
  {
    var viewModel = NewViewModel();
    viewModel.PostType = PostComposeTypes.DataPoint;
    viewModel.Title = "Approval data";
    viewModel.Markdown = "Approved with a strong credit profile.";
    viewModel.DataPointVertical = "credit_card";
    viewModel.StructuredDataJson = $$"""
      {
        "vertical": "credit_card",
        "schema_version": 1,
        "topic_ids": ["{{TopicId}}"],
        "result": "approved"
      }
      """;

    Assert.Equal(
        "Structured data must match the selected data point vertical schema.",
        viewModel.Validation.Message);

    viewModel.StructuredDataJson = $$"""
      {
        "vertical": "credit_card",
        "schema_version": 1,
        "topic_ids": ["{{TopicId}}"],
        "result": "approved",
        "credit_score_range": "740-799"
      }
      """;

    Assert.True(viewModel.Validation.CanPublish);
  }

  [Fact]
  public void DataPointStructuredDataMustMatchBankAccountSchema()
  {
    var viewModel = NewViewModel();
    viewModel.PostType = PostComposeTypes.DataPoint;
    viewModel.Title = "Account data";
    viewModel.Markdown = "Approved for a new checking account.";
    viewModel.DataPointVertical = "bank_account";
    viewModel.StructuredDataJson = $$"""
      {
        "vertical": "bank_account",
        "schema_version": 1,
        "topic_ids": ["{{TopicId}}"],
        "result": "approved"
      }
      """;

    Assert.True(viewModel.Validation.CanPublish);

    viewModel.StructuredDataJson = $$"""
      {
        "vertical": "bank_account",
        "schema_version": 1,
        "topic_ids": ["not-a-uuid"],
        "result": "approved"
      }
      """;

    Assert.Equal(
        "Structured data must match the selected data point vertical schema.",
        viewModel.Validation.Message);
  }

  private static PostComposeViewModel NewViewModel(ISessionStore? sessionStore = null) =>
      new(
          new UnsupportedPostsService(),
          new AppConfig(new Uri("https://api.example.test"), "site-key", true),
          sessionStore: sessionStore);

  private const string ValidReviewMarkdown =
      "This review gives enough detail about the card application experience. " +
      "It explains the approval timeline, the credit profile, and the reasons the offer was useful. " +
      "It also includes enough words for the server side review quality thresholds.";

  private sealed class UnsupportedPostsService : IPostsService
  {
    public Task<PostsFeedResponse> FetchFeedAsync(
        FetchPostsFeedRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostsFeedResponse> FetchPostsAsync(
        FetchPostsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostResponse> FetchPostAsync(
        string postIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task VotePostAsync(string postId, int score, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UpdatePostAsync(
        string postIdOrSlug,
        UpdatePostBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> ArchivePostAsync(
        string postIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UnarchivePostAsync(
        string postIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class StubSessionStore : ISessionStore
  {
    public StubSessionStore(IReadOnlyList<string> roles) =>
        Current = new SessionSnapshot(new User("user-1", "user", Roles: roles));

    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
