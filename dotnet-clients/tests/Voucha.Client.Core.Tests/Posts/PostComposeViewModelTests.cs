using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Relations;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostComposeViewModelTests
{
  private const string TopicId = "01234567-89ab-7def-0123-456789abcdef";
  private const string ValidReviewMarkdown =
      "This review gives enough detail about the card application experience. " +
      "It explains the approval timeline, the credit profile, and the reasons the offer was useful. " +
      "It also includes enough words for the server side review quality thresholds.";

  [Fact]
  public void ValidationRequiresCaptchaUnlessBypassEnabled()
  {
    var viewModel = NewViewModel(new AppConfig(new Uri("https://api.example.test"), "site-key"));
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";

    Assert.False(viewModel.Validation.CanPublish);
    Assert.Equal("Captcha token is required.", viewModel.Validation.Message);
    Assert.True(viewModel.IsValidExceptCaptcha);

    var bypassViewModel = NewViewModel(new AppConfig(
        new Uri("https://api.example.test"),
        "site-key",
        AllowPostComposeCaptchaBypass: true));
    bypassViewModel.Title = "Title";
    bypassViewModel.Markdown = "Body";

    Assert.True(bypassViewModel.Validation.CanPublish);
  }

  [Fact]
  public async Task PublishAsyncCreatesCommunityPostWhenCommunitySlugIsSet()
  {
    var service = new RecordingPostsService();
    var viewModel = NewViewModel(new AppConfig(
        new Uri("https://api.example.test"),
        "site-key",
        AllowPostComposeCaptchaBypass: true), service);
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.CommunitySlug = "travel";
    viewModel.PostType = PostComposeTypes.Discussion;
    viewModel.SetImages([new PostComposeImageDraft("image-1", 0, "Caption")]);

    var published = await viewModel.PublishAsync(TestContext.Current.CancellationToken);

    Assert.True(published);
    Assert.Null(service.GlobalBody);
    Assert.Equal("travel", service.CommunitySlug);
    Assert.Equal(PostComposeTypes.Discussion, service.CommunityBody?.PostType);
    Assert.Equal("image-1", Assert.Single(service.CommunityBody?.Images ?? []).ImageId);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task PublishAsyncCreatesGlobalPostWithDerivedRowsAndRelations()
  {
    var service = new RecordingPostsService();
    var relations = new RecordingRelationsService();
    var viewModel = NewViewModel(new AppConfig(
        new Uri("https://api.example.test"),
        "site-key",
        AllowPostComposeCaptchaBypass: true), service, relations);
    viewModel.PostType = PostComposeTypes.Review;
    viewModel.Title = "Review";
    viewModel.Markdown = ValidReviewMarkdown;
    viewModel.ReviewTopicId = TopicId;
    viewModel.ReviewRating = "5";
    viewModel.RelatedLinkIdentifier = "url-1";
    viewModel.ImageId = "image-1";
    viewModel.ImageCaption = "Alt";
    viewModel.DeclaredLanguage = "en";
    viewModel.HpWebsite = "bot";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.Equal(PostComposeTypes.Review, service.GlobalBody?.PostType);
    Assert.Null(service.GlobalBody?.TurnstileToken);
    Assert.Equal("en", service.GlobalBody?.DeclaredLanguage);
    Assert.Equal("bot", service.GlobalBody?.HpWebsite);
    Assert.Equal(5, Assert.Single(service.GlobalBody?.ReviewTopicRatings ?? []).Rating);
    Assert.Equal("image-1", Assert.Single(service.GlobalBody?.Images ?? []).ImageId);
    Assert.True(viewModel.HasPublished);
    Assert.Collection(relations.Requests, request => Assert.Equal("url-1", request.ObjectId));
  }

  [Fact]
  public async Task PublishAsyncReturnsFalseWhilePublishIsAlreadyRunning()
  {
    var service = new BlockingPostsService();
    var viewModel = NewViewModel(
        new AppConfig(new Uri("https://api.example.test"), "site-key", true),
        service);
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";

    var firstPublish = viewModel.PublishAsync(TestContext.Current.CancellationToken);
    await service.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
    var secondPublish = await viewModel.PublishAsync(TestContext.Current.CancellationToken);

    Assert.False(secondPublish);
    Assert.Equal("Publishing is in progress.", viewModel.Validation.Message);
    service.Release.SetResult();
    Assert.True(await firstPublish);
  }

  [Fact]
  public async Task PublishAsyncKeepsPostSuccessWhenRelationCreationFails()
  {
    var viewModel = NewViewModel(
        new AppConfig(new Uri("https://api.example.test"), "site-key", true),
        relations: new ThrowingRelationsService());
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.RelatedLinkIdentifier = "url-1";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.True(viewModel.HasPublished);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task PublishAsyncSplitsVisibleRelatedUrlIdentifiers()
  {
    var relations = new RecordingRelationsService();
    var viewModel = NewViewModel(
        new AppConfig(new Uri("https://api.example.test"), "site-key", true),
        relations: relations);
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.RelatedLinkIdentifier = " url-1, url-2 ,, url-3 ";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.Collection(
        relations.Requests,
        request => Assert.Equal("url-1", request.ObjectId),
        request => Assert.Equal("url-2", request.ObjectId),
        request => Assert.Equal("url-3", request.ObjectId));
  }

  [Fact]
  public void ValidationCoversPostTypeSpecificFailures()
  {
    var viewModel = NewViewModel(new AppConfig(
        new Uri("https://api.example.test"),
        "site-key",
        AllowPostComposeCaptchaBypass: true));
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";

    viewModel.PostType = PostComposeTypes.DataPoint;
    viewModel.ReviewRating = "bad";
    Assert.Equal("Data point vertical is required.", viewModel.Validation.Message);

    viewModel.DataPointVertical = "credit_card";
    Assert.Equal("Structured data is required for data point posts.", viewModel.Validation.Message);

    viewModel.StructuredDataJson = "{";
    Assert.Equal("Structured data must be valid JSON.", viewModel.Validation.Message);

    viewModel.PostType = PostComposeTypes.Article;
    viewModel.StructuredDataJson = "{}";
    Assert.Equal("Only administrators can publish this post type.", viewModel.Validation.Message);
  }

  [Fact]
  public void ValidationRejectsInvalidLinksAndResetsUnsupportedCommunityTypes()
  {
    var viewModel = NewViewModel(new AppConfig(new Uri("https://api.example.test"), "site-key", true));
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.PostType = PostComposeTypes.Link;
    viewModel.LinkAddress = "www.example.test";

    Assert.Equal("A valid HTTPS link URL or UUID URL ID is required.", viewModel.Validation.Message);

    viewModel.LinkAddress = "https://example.test";
    Assert.True(viewModel.Validation.CanPublish);

    viewModel.LinkIdentifier = "not-a-uuid";
    Assert.Equal("A valid HTTPS link URL or UUID URL ID is required.", viewModel.Validation.Message);

    viewModel.LinkIdentifier = TopicId;
    Assert.True(viewModel.Validation.CanPublish);

    viewModel.CommunitySlug = "travel";
    Assert.Equal(PostComposeTypes.Discussion, viewModel.PostType);
    Assert.True(viewModel.Validation.CanPublish);
  }

  [Fact]
  public void ValidationRejectsInvalidAudienceValues()
  {
    var viewModel = NewViewModel(new AppConfig(new Uri("https://api.example.test"), "site-key", true));
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";

    viewModel.Broadcast = "Public";
    Assert.Equal("Broadcast or privacy is invalid.", viewModel.Validation.Message);

    viewModel.Broadcast = "followers";
    viewModel.Privacy = "hidden";
    Assert.Equal("Broadcast or privacy is invalid.", viewModel.Validation.Message);

    viewModel.Privacy = "private";
    Assert.True(viewModel.Validation.CanPublish);
  }

  [Fact]
  public async Task PublishAsyncOnlySendsLinkFieldsForLinkPosts()
  {
    var service = new RecordingPostsService();
    var viewModel = NewViewModel(
        new AppConfig(new Uri("https://api.example.test"), "site-key", true),
        service);
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.LinkAddress = "https://example.test";
    viewModel.LinkIdentifier = "url-1";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.Null(service.GlobalBody?.Url);
    Assert.Null(service.GlobalBody?.LinkIdentifier);

    viewModel.ResetDraft();
    viewModel.PostType = PostComposeTypes.Link;
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.LinkAddress = "https://example.test";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal(new Uri("https://example.test"), service.GlobalBody?.Url);
  }

  [Fact]
  public async Task PublishAsyncOnlySendsReviewAndStructuredFieldsForMatchingTypes()
  {
    var service = new RecordingPostsService();
    var viewModel = NewViewModel(
        new AppConfig(new Uri("https://api.example.test"), "site-key", true),
        service);
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.ReviewTopicId = "topic-1";
    viewModel.ReviewRating = "5";
    viewModel.DataPointVertical = "credit_card";
    viewModel.StructuredDataJson = "{broken";

    Assert.True(viewModel.Validation.CanPublish);
    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.Null(service.GlobalBody?.ReviewTopicRatings);
    Assert.Null(service.GlobalBody?.DataPointVertical);
    Assert.Null(service.GlobalBody?.StructuredData);

    viewModel.ResetDraft();
    viewModel.PostType = PostComposeTypes.DataPoint;
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.DataPointVertical = "credit_card";
    viewModel.StructuredDataJson = $$"""
      {
        "vertical": "credit_card",
        "schema_version": 1,
        "topic_ids": ["{{TopicId}}"],
        "result": "approved",
        "credit_score_range": "740-799"
      }
      """;

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal("credit_card", service.GlobalBody?.DataPointVertical);
    Assert.NotNull(service.GlobalBody?.StructuredData);
  }

  [Fact]
  public void AdminSessionCanUseArticleAndBlogPostTypes()
  {
    var viewModel = new PostComposeViewModel(
        new RecordingPostsService(),
        new AppConfig(new Uri("https://api.example.test"), "site-key", true),
        sessionStore: new StubSessionStore(["administrator"]));

    Assert.Contains(PostComposeTypes.Article, viewModel.AvailablePostTypes);
    Assert.Contains(PostComposeTypes.Blog, viewModel.AvailablePostTypes);
    Assert.Equal("blog_post", PostComposeTypes.Blog);
    viewModel.PostType = PostComposeTypes.Blog;
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";

    Assert.True(viewModel.Validation.CanPublish);
  }

  [Fact]
  public void ResetDraftClearsInputsCollectionsAndState()
  {
    var viewModel = NewViewModel(new AppConfig(new Uri("https://api.example.test"), "site-key", true));
    viewModel.PostType = PostComposeTypes.Link;
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.CommunitySlug = "community";
    viewModel.LinkAddress = "https://example.test";
    viewModel.SetReviewTopicRatings([new PostComposeTopicRatingDraft("topic-1", 4)]);
    viewModel.SetDiscussionCategories([new PostComposeCategoryDraft(PostComposeCategoryKind.Topic, "topic-2")]);
    viewModel.SetRelatedUrls([new PostComposeRelatedUrlDraft("url-1")]);
    viewModel.SetImages([new PostComposeImageDraft("image-1", 0)]);

    viewModel.ResetDraft();

    Assert.Equal(PostComposeTypes.Discussion, viewModel.PostType);
    Assert.Empty(viewModel.Title);
    Assert.Empty(viewModel.Markdown);
    Assert.False(viewModel.IsCommunityPost);
    Assert.Empty(viewModel.ReviewTopicRatings);
    Assert.Empty(viewModel.DiscussionCategories);
    Assert.Empty(viewModel.RelatedUrls);
    Assert.Empty(viewModel.Images);
  }

  [Fact]
  public async Task PublishAsyncSurfacesValidationAndServiceErrors()
  {
    var viewModel = NewViewModel(new AppConfig(new Uri("https://api.example.test"), "site-key"));

    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal(LoadState.Error, viewModel.State);

    var throwingViewModel = NewViewModel(
        new AppConfig(new Uri("https://api.example.test"), "site-key", true),
        new ThrowingPostsService());
    throwingViewModel.Title = "Title";
    throwingViewModel.Markdown = "Body";

    Assert.False(await throwingViewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal("No posts today.", throwingViewModel.ErrorMessage);
  }

  private static PostComposeViewModel NewViewModel(
      AppConfig config,
      RecordingPostsService? service = null,
      RecordingRelationsService? relations = null) =>
      new(service ?? new RecordingPostsService(), config, relations);

  private class RecordingPostsService : IPostsService
  {
    public CreatePostBody? GlobalBody { get; private set; }

    public string? CommunitySlug { get; private set; }

    public CreatePostBody? CommunityBody { get; private set; }

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

    public virtual Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      GlobalBody = body;
      return Task.FromResult(MakeResponse());
    }

    public virtual Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      CommunitySlug = communityIdOrSlug;
      CommunityBody = body;
      return Task.FromResult(MakeResponse());
    }

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

    private static PostMutationResponse MakeResponse() =>
        new(new Post("post-1", "discussion", "Title", "Body", "user-1"));
  }

  private sealed class ThrowingPostsService : RecordingPostsService
  {
    public override Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        Task.FromException<PostMutationResponse>(new VouchaApiException("No posts today."));
  }

  private sealed class BlockingPostsService : RecordingPostsService
  {
    public TaskCompletionSource Started { get; } = new();

    public TaskCompletionSource Release { get; } = new();

    public override async Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      Started.SetResult();
      await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      return await base.CreatePostAsync(body, idempotencyKey, cancellationToken).ConfigureAwait(false);
    }
  }

  private class RecordingRelationsService : IEntityRelationsService
  {
    public List<CreateEntityRelationRequest> Requests { get; } = [];

    public Task<EntityRelationsResponse> FetchAsync(
        EntityRelationsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public virtual Task<EntityRelationResponse> CreateAsync(
        CreateEntityRelationRequest request,
        CancellationToken cancellationToken = default)
    {
      Requests.Add(request);
      return Task.FromResult(new EntityRelationResponse(new EntityRelation("relation-1")));
    }
  }

  private sealed class ThrowingRelationsService : RecordingRelationsService
  {
    public override Task<EntityRelationResponse> CreateAsync(
        CreateEntityRelationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromException<EntityRelationResponse>(new VouchaApiException("Relation unavailable."));
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
