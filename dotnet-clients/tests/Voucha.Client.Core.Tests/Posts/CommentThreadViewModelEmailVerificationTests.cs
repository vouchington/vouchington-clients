using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class CommentThreadViewModelTests
{
  [Fact]
  public async Task VoteRequestsEmailRecoveryWithoutReloadingWhenVerificationIsRequired()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root body", createdAt: Now(0));
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(),
      VoteException = new VouchaApiException(
          System.Net.HttpStatusCode.Forbidden,
          "{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}"),
    };
    var viewModel = new CommentThreadViewModel(service, service, root.Id, "user-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var rootFetches = service.RootFetchCount;

    await viewModel.VotePostAsync(root.Id, ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(rootFetches, service.RootFetchCount);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
    Assert.False(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ReplyRequestsEmailRecoveryWithoutReloadingWhenVerificationIsRequired()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root body", createdAt: Now(0));
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(),
      CreatePostException = new VouchaApiException(
          System.Net.HttpStatusCode.Forbidden,
          "{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}"),
    };
    var viewModel = new CommentThreadViewModel(service, service, root.Id, "user-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var rootFetches = service.RootFetchCount;

    await viewModel.ReplyAsync(
        root.Id,
        "Keep this reply",
        turnstileToken: "turnstile",
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(rootFetches, service.RootFetchCount);
    Assert.Equal("Keep this reply", service.CreatePostMarkdown);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ReplyRotatesItsKeyAfterAnIdempotencyReuseResponse()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root body", createdAt: Now(0));
    var service = ReplyService(root, "IDEMPOTENCY_KEY_REUSED");
    var viewModel = new CommentThreadViewModel(service, service, root.Id, "user-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(await viewModel.ReplyAsync(root.Id, "Unchanged reply", cancellationToken: TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ReplyAsync(root.Id, "Unchanged reply", cancellationToken: TestContext.Current.CancellationToken));

    Assert.Equal(2, service.CreatePostIdempotencyKeys.Count);
    Assert.NotEqual(service.CreatePostIdempotencyKeys[0], service.CreatePostIdempotencyKeys[1]);
  }

  [Fact]
  public async Task ReplyRetainsItsKeyAfterAnInProgressResponse()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root body", createdAt: Now(0));
    var service = ReplyService(root, "CONTRIBUTION_ADMISSION_IN_PROGRESS");
    var viewModel = new CommentThreadViewModel(service, service, root.Id, "user-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(await viewModel.ReplyAsync(root.Id, "Unchanged reply", cancellationToken: TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ReplyAsync(root.Id, "Unchanged reply", cancellationToken: TestContext.Current.CancellationToken));

    Assert.Equal(2, service.CreatePostIdempotencyKeys.Count);
    Assert.Equal(service.CreatePostIdempotencyKeys[0], service.CreatePostIdempotencyKeys[1]);
  }

  private static RecordingPostsService ReplyService(Post root, string errorCode)
  {
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(),
    };
    service.CreatePostExceptions.Enqueue(new VouchaApiException(
        System.Net.HttpStatusCode.Conflict,
        $"{{\"code\":\"{errorCode}\"}}"));
    return service;
  }
}
