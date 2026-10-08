using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Contributions;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostComposeViewModelPublishTests
{
  [Fact]
  public void ContributionIdentityRotatesWhenADraftChangesAndThenReturnsToItsEarlierContent()
  {
    var identity = new ContributionRequestIdentity();

    var first = identity.KeyFor("post", "intent-a");
    var changed = identity.KeyFor("post", "intent-b");
    var restored = identity.KeyFor("post", "intent-a");
    var independent = identity.KeyFor("story-discussion\u001fstory-1", "intent-a");

    Assert.NotEqual(first, changed);
    Assert.NotEqual(first, restored);
    Assert.NotEqual(changed, restored);
    Assert.Equal(restored, identity.KeyFor("post", "intent-a"));
    Assert.Equal(independent, identity.KeyFor("story-discussion\u001fstory-1", "intent-a"));
  }

  [Fact]
  public async Task ContributionIdentityReturnsOneKeyWhenTheSameDraftIsRequestedConcurrently()
  {
    var identity = new ContributionRequestIdentity();
    var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var requests = Enumerable.Range(0, 100)
        .Select(async _ =>
        {
          await start.Task;
          return identity.KeyFor("story-discussion\u001fstory-1", "intent-a");
        })
        .ToArray();

    start.SetResult();
    var keys = await Task.WhenAll(requests);

    Assert.Single(keys.Distinct());
  }

  [Fact]
  public async Task PublishAsyncOmitsSlugForNonAdminUsers()
  {
    var service = new RecordingPostsService();
    var viewModel = new PostComposeViewModel(
        service,
        new AppConfig(new Uri("https://api.example.test"), "site-key", true),
        sessionStore: new StubSessionStore([]));
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.Slug = "draft-slug";
    viewModel.TurnstileToken = "token";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Null(service.GlobalBody?.Slug);
  }

  [Fact]
  public async Task PublishAsyncClearsTurnstileTokenAfterServiceFailure()
  {
    var viewModel = new PostComposeViewModel(
        new ThrowingPostsService(),
        new AppConfig(new Uri("https://api.example.test"), "site-key", true));
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.TurnstileToken = "spent-token";

    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Empty(viewModel.TurnstileToken);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

  [Fact]
  public async Task PublishAsyncSendsAtomicallyAuthoredDiscussionCategories()
  {
    var service = new RecordingPostsService();
    var viewModel = new PostComposeViewModel(
        service,
        new AppConfig(new Uri("https://api.example.test"), "site-key", true));
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.DiscussionCategoryTopicId = "topic-1";
    viewModel.AddDiscussionCategoryTopic();
    viewModel.DiscussionCategoryHashtag = " #Me.Too__2026 ";
    viewModel.AddDiscussionCategoryHashtag();

    Assert.Equal("topic-1", viewModel.DiscussionCategories[0].UserContentValue);
    Assert.Equal("#Me.Too__2026", viewModel.DiscussionCategories[1].UserContentValue);
    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.NotNull(service.GlobalBody?.Categories);
    Assert.Collection(
        service.GlobalBody!.Categories!,
        category => Assert.Equal("topic-1", Assert.IsType<TopicPostCategoryInput>(category).TopicId),
        category => Assert.Equal("#Me.Too__2026", Assert.IsType<HashtagPostCategoryInput>(category).Hashtag));
  }

  [Fact]
  public void AddDiscussionCategoryHashtagRejectsOverlongAuthoredTokenEvenWhenItWouldCollapse()
  {
    var viewModel = new PostComposeViewModel(
        new RecordingPostsService(),
        new AppConfig(new Uri("https://api.example.test"), "site-key", true));
    viewModel.DiscussionCategoryHashtag = $"a{new string('.', 255)}b";

    viewModel.AddDiscussionCategoryHashtag();

    Assert.Empty(viewModel.DiscussionCategories);
  }

  [Fact]
  public async Task PublishAsyncPreservesDraftAndRequestsEmailRecovery()
  {
    var service = new ThrowingPostsService(new VouchaApiException(
        System.Net.HttpStatusCode.Forbidden,
        "{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}"));
    var viewModel = new PostComposeViewModel(
        service,
        new AppConfig(new Uri("https://api.example.test"), "site-key", true))
    {
      Title = "Keep title",
      Markdown = "Keep body",
      TurnstileToken = "spent-token",
    };

    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.Equal("Keep title", viewModel.Title);
    Assert.Equal("Keep body", viewModel.Markdown);
    Assert.Empty(viewModel.TurnstileToken);
    Assert.Equal(1, service.CreateCalls);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
    Assert.Equal(1, service.CreateCalls);
  }

  [Fact]
  public void ReportTurnstileChallengeFailureSetsVisibleError()
  {
    var viewModel = new PostComposeViewModel(
        new RecordingPostsService(),
        new AppConfig(new Uri("https://api.example.test"), "site-key", true));

    viewModel.ReportTurnstileChallengeFailure("Missing VOUCHA_TURNSTILE_SITE_KEY");

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Equal("Missing VOUCHA_TURNSTILE_SITE_KEY", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task PublishAsyncReusesTheDraftKeyAfterFailureAndRotatesAfterSuccess()
  {
    var service = new RetryThenSucceedPostsService();
    var viewModel = new PostComposeViewModel(
        service,
        new AppConfig(new Uri("https://api.example.test"), "site-key", true))
    {
      Title = "Title",
      Markdown = "Draft",
    };

    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    var first = service.IdempotencyKeys.Single();
    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal(first, service.IdempotencyKeys[1]);
    viewModel.Title = "Changed title";
    viewModel.Markdown = "Changed draft";
    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.Equal(3, service.IdempotencyKeys.Count);
    Assert.NotEqual(first, service.IdempotencyKeys[2]);
    Assert.All(service.IdempotencyKeys, key => Assert.True(Guid.TryParse(key, out _)));
  }

  [Fact]
  public async Task PublishAsyncRotatesTheDraftKeyAfterAnIdempotencyReuseResponse()
  {
    var service = new KeyReuseThenSucceedPostsService();
    var viewModel = new PostComposeViewModel(
        service,
        new AppConfig(new Uri("https://api.example.test"), "site-key", true))
    {
      Title = "Title",
      Markdown = "Draft",
    };

    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.Equal(2, service.IdempotencyKeys.Count);
    Assert.NotEqual(service.IdempotencyKeys[0], service.IdempotencyKeys[1]);
  }

  [Fact]
  public async Task PublishAsyncRotatesTheDraftKeyWhenTheCommunityDestinationChanges()
  {
    var service = new RecordingPostsService();
    var viewModel = new PostComposeViewModel(
        service,
        new AppConfig(new Uri("https://api.example.test"), "site-key", true))
    {
      Title = "Title",
      Markdown = "Draft",
      CommunitySlug = "community-a",
    };

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    var first = service.IdempotencyKeys.Single();
    viewModel.CommunitySlug = "community-b";
    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.NotEqual(first, service.IdempotencyKeys[1]);
  }

  [Fact]
  public async Task PublishAsyncRotatesFailedCommunityIntentAndPreservesTheNewDestinationRetry()
  {
    var service = new FailedCommunityPostsService();
    var viewModel = new PostComposeViewModel(
        service,
        new AppConfig(new Uri("https://api.example.test"), "site-key", true))
    {
      Title = "Same title",
      Markdown = "Same draft",
      CommunitySlug = "community-a",
    };

    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal(LoadState.Error, viewModel.State);
    viewModel.CommunitySlug = " community-b ";
    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.Collection(service.Attempts,
        request => Assert.Equal("community-a", request.Destination),
        request => Assert.Equal("community-b", request.Destination),
        request => Assert.Equal("community-b", request.Destination));
    Assert.NotEqual(service.Attempts[0].Key, service.Attempts[1].Key);
    Assert.Equal(service.Attempts[1].Key, service.Attempts[2].Key);
    Assert.All(service.Attempts, request =>
    {
      Assert.False(string.IsNullOrWhiteSpace(request.Key));
      Assert.Equal("Same title", request.Body.Title);
      Assert.Equal("Same draft", request.Body.Markdown);
    });
  }

  private sealed class FailedCommunityPostsService : RecordingPostsService
  {
    public List<(string Destination, CreatePostBody Body, string Key)> Attempts { get; } = [];

    public override Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      Attempts.Add((communityIdOrSlug, body, idempotencyKey));
      return Task.FromException<PostMutationResponse>(new HttpRequestException("Connection interrupted."));
    }
  }

  private class RecordingPostsService : IPostsService
  {
    public List<string> IdempotencyKeys { get; } = [];
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
      IdempotencyKeys.Add(idempotencyKey);
      return Task.FromResult(new PostMutationResponse(new Post("post-1", "discussion", "Title", "Body", "user-1")));
    }

    public virtual Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      CommunitySlug = communityIdOrSlug;
      CommunityBody = body;
      IdempotencyKeys.Add(idempotencyKey);
      return Task.FromResult(new PostMutationResponse(new Post("post-1", "discussion", "Title", "Body", "user-1")));
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
  }

  private sealed class RetryThenSucceedPostsService : RecordingPostsService
  {
    private bool shouldFail = true;

    public override Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      if (shouldFail)
      {
        shouldFail = false;
        IdempotencyKeys.Add(idempotencyKey);
        return Task.FromException<PostMutationResponse>(new VouchaApiException(
            System.Net.HttpStatusCode.TooManyRequests,
            "{\"code\":\"CONTRIBUTION_QUOTA_EXCEEDED\"}"));
      }
      return base.CreatePostAsync(body, idempotencyKey, cancellationToken);
    }
  }

  private sealed class KeyReuseThenSucceedPostsService : RecordingPostsService
  {
    private bool shouldFail = true;

    public override Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      if (shouldFail)
      {
        shouldFail = false;
        IdempotencyKeys.Add(idempotencyKey);
        return Task.FromException<PostMutationResponse>(new VouchaApiException(
            System.Net.HttpStatusCode.Conflict,
            "{\"code\":\"IDEMPOTENCY_KEY_REUSED\"}"));
      }
      return base.CreatePostAsync(body, idempotencyKey, cancellationToken);
    }
  }

  private sealed class ThrowingPostsService(Exception? failure = null) : IPostsService
  {
    private readonly Exception failure = failure ?? new VouchaApiException("No posts today.");

    public int CreateCalls { get; private set; }

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
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      CreateCalls++;
      return Task.FromException<PostMutationResponse>(failure);
    }

    public Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      CreateCalls++;
      return Task.FromException<PostMutationResponse>(failure);
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
