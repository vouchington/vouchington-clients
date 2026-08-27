using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class StaffSupportThreadViewModelTests
{
  [Theory]
  [InlineData(SupportThreadStatus.Open, true, true, false)]
  [InlineData(SupportThreadStatus.Assigned, false, true, false)]
  [InlineData(SupportThreadStatus.Resolved, false, false, true)]
  [InlineData(SupportThreadStatus.Closed, false, false, false)]
  public async Task LifecycleCapabilitiesFollowThreadStatus(
      SupportThreadStatus status,
      bool canAssign,
      bool canResolve,
      bool canReopen)
  {
    var viewModel = new StaffSupportThreadViewModel(new FakeStaffSupportService(status), "administrator");

    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);

    Assert.Equal(canAssign, viewModel.CanAssign);
    Assert.Equal(canResolve, viewModel.CanResolve);
    Assert.Equal(canReopen, viewModel.CanReopen);
    Assert.Equal(canResolve, viewModel.CanReply);
  }

  [Fact]
  public async Task ReplyFailureIsRenderedAndPreservesTheReply()
  {
    var service = new FakeStaffSupportService(SupportThreadStatus.Open) { CreateFailure = new HttpRequestException("offline") };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);
    viewModel.ReplyText = "Please try this.";

    await viewModel.SaveOutboundReplyAsync(TestContext.Current.CancellationToken);

    Assert.Equal("offline", viewModel.ErrorMessage);
    Assert.Equal("Please try this.", viewModel.ReplyText);
    Assert.False(viewModel.IsBusy);
  }

  [Fact]
  public async Task RetryLoadClearsTheInitialFailureAndLoadsThreadDetails()
  {
    var service = new FakeStaffSupportService(SupportThreadStatus.Open) { FetchFailure = new HttpRequestException("offline") };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");

    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);

    Assert.Equal("offline", viewModel.ErrorMessage);
    Assert.Null(viewModel.Thread);
    service.FetchFailure = null;

    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);

    Assert.Equal(2, service.FetchThreadCount);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal("thread", viewModel.Thread?.Id);
  }

  [Fact]
  public async Task ThreadActionsRemainDisabledUntilMessageHistoryFinishesLoading()
  {
    var service = new FakeStaffSupportService(SupportThreadStatus.Open) { PauseMessages = true };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");

    var load = viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);
    await service.MessagesStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.Thread);
    Assert.False(viewModel.CanReply);
    Assert.True(viewModel.IsBusy);
    service.ReleaseMessages.SetResult();
    await load;

    Assert.Equal("thread", viewModel.Thread?.Id);
    Assert.True(viewModel.CanReply);
    Assert.False(viewModel.IsBusy);
  }

  [Fact]
  public async Task ConcurrentReplyTapIsIgnoredWhileMutationIsRunning()
  {
    var service = new FakeStaffSupportService(SupportThreadStatus.Open) { PauseCreate = true };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);
    viewModel.ReplyText = "Only once";

    var first = viewModel.SaveOutboundReplyAsync(TestContext.Current.CancellationToken);
    await service.CreateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    var second = viewModel.SaveOutboundReplyAsync(TestContext.Current.CancellationToken);
    service.ReleaseCreate.SetResult();
    await Task.WhenAll(first, second);

    Assert.Equal(1, service.CreateCount);
  }

  [Fact]
  public async Task AnonymousStaffSurfaceDisablesMutationsAndShowsAnAuthorizationError()
  {
    var service = new FakeStaffSupportService(SupportThreadStatus.Open);
    var viewModel = new StaffSupportThreadViewModel(service, null);

    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);
    await viewModel.AssignAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, service.FetchThreadCount);
    Assert.Null(viewModel.Thread);
    Assert.False(viewModel.CanAssign);
    Assert.False(viewModel.CanResolve);
    Assert.False(viewModel.CanReply);
    Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
  }

  private sealed class FakeStaffSupportService(SupportThreadStatus status) : IStaffSupportService
  {
    public Exception? CreateFailure { get; init; }
    public Exception? FetchFailure { get; set; }
    public bool PauseCreate { get; init; }
    public bool PauseMessages { get; init; }
    public int CreateCount { get; private set; }
    public int FetchThreadCount { get; private set; }
    public TaskCompletionSource CreateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource MessagesStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseMessages { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<SupportThreadResponse> FetchThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
      FetchThreadCount++;
      return FetchFailure is null
          ? Task.FromResult(new SupportThreadResponse(Thread(status)))
          : Task.FromException<SupportThreadResponse>(FetchFailure);
    }
    public async Task<SupportMessageListResponse> FetchMessagesAsync(string threadId, string? after, int limit, CancellationToken cancellationToken = default)
    {
      MessagesStarted.TrySetResult();
      if (PauseMessages) await ReleaseMessages.Task.WaitAsync(cancellationToken);
      return new SupportMessageListResponse([], new PageInfo(null, false, null));
    }
    public async Task<SupportMessageResponse> CreateMessageAsync(string threadId, string bodyText, CancellationToken cancellationToken = default)
    {
      CreateCount++;
      CreateStarted.TrySetResult();
      if (PauseCreate) await ReleaseCreate.Task.WaitAsync(cancellationToken);
      if (CreateFailure is not null) throw CreateFailure;
      return new SupportMessageResponse(Message(bodyText));
    }

    public Task<SupportThreadListResponse> FetchThreadsAsync(string? query, StaffSupportThreadStatusFilter? threadStatus, string? after, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SupportThreadResponse> AssignAsync(string threadId, string administratorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SupportThreadResponse> ResolveAsync(string threadId, bool resolved, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SupportDraftQueuedResponse> QueueDraftAsync(string threadId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SupportMessageResponse> UpdateDraftAsync(string threadId, string messageId, string bodyText, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SupportMessageResponse> ApproveAsync(string threadId, string messageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SupportMessageResponse> SendAsync(string threadId, string messageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SupportContactListResponse> FetchContactsAsync(string? query, string? after, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SupportContactDetailResponse> FetchContactAsync(string contactId, string? after, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    private static SupportThread Thread(SupportThreadStatus threadStatus) => new(
      "thread", "contact", "Subject", null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
      null, null, null, null, threadStatus);
    private static SupportMessage Message(string body) => new(
      "message", "thread", "outbound", body, body, DateTimeOffset.UnixEpoch, null,
      DateTimeOffset.UnixEpoch, null, null, null, null, DateTimeOffset.UnixEpoch, null, null, null, null, null);
  }
}
