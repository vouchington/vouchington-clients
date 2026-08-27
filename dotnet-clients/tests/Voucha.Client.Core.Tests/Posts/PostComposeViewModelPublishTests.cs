using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostComposeViewModelPublishTests
{
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

  private sealed class RecordingPostsService : IPostsService
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

    public Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        CancellationToken cancellationToken = default)
    {
      GlobalBody = body;
      return Task.FromResult(new PostMutationResponse(new Post("post-1", "discussion", "Title", "Body", "user-1")));
    }

    public Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        CancellationToken cancellationToken = default)
    {
      CommunitySlug = communityIdOrSlug;
      CommunityBody = body;
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
        CancellationToken cancellationToken = default)
    {
      CreateCalls++;
      return Task.FromException<PostMutationResponse>(failure);
    }

    public Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
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
