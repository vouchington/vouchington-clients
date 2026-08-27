using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatListLoadTests
{
  [Fact]
  public async Task ChatListViewModelResolveConversationTitlePagesUntilItFindsTheConversation()
  {
    var service = new FakeChatService();
    service.FetchMyConversationsAsyncOverride = (after, _, _) =>
    {
      var response = after is null
          ? new ChatConversationListResponse(
              [
                new ChatConversation(
                    "conversation-1",
                    "Ignored",
                    DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                    "user-1",
                    DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                    null,
                    null,
                    null),
              ],
              new PageInfo("cursor-1", true, null))
          : new ChatConversationListResponse(
              [
                new ChatConversation(
                    "conversation-2",
                    "Resolved title",
                    DateTimeOffset.Parse("2026-07-01T10:01:00Z"),
                    "user-1",
                    DateTimeOffset.Parse("2026-07-01T10:01:00Z"),
                    null,
                    null,
                    null),
              ],
              new PageInfo(null, false, null));
      return Task.FromResult(response);
    };

    var title = await ChatListViewModel.ResolveConversationTitleAsync(
        service,
        "conversation-2",
        TestContext.Current.CancellationToken);

    Assert.Equal("Resolved title", title);
    Assert.Equal(2, service.FetchMyConversationsCount);
  }

  [Fact]
  public async Task ChatListViewModelResolveConversationTitleContinuesPastTenPages()
  {
    var service = new FakeChatService();
    var calls = 0;
    service.FetchMyConversationsAsyncOverride = (_, _, _) =>
    {
      calls += 1;
      var page = calls;
      var isMatch = page == 11;
      return Task.FromResult(
          new ChatConversationListResponse(
              [
                new ChatConversation(
                    $"conversation-{page}",
                    isMatch ? "Deep title" : "Ignored",
                    DateTimeOffset.Parse($"2026-07-01T10:{page:00}:00Z"),
                    "user-1",
                    DateTimeOffset.Parse($"2026-07-01T10:{page:00}:00Z"),
                    null,
                    null,
                    null),
              ],
              new PageInfo(isMatch ? null : $"cursor-{page}", !isMatch, null)));
    };

    var title = await ChatListViewModel.ResolveConversationTitleAsync(
        service,
        "conversation-11",
        TestContext.Current.CancellationToken);

    Assert.Equal("Deep title", title);
    Assert.Equal(11, calls);
  }

  [Fact]
  public async Task ChatListViewModelResolveConversationTitleReturnsNullWhenLookupFails()
  {
    var service = new FakeChatService
    {
      FetchMyConversationsError = new InvalidOperationException("list failed"),
    };

    var title = await ChatListViewModel.ResolveConversationTitleAsync(
        service,
        "conversation-2",
        TestContext.Current.CancellationToken);

    Assert.Null(title);
  }

  [Fact]
  public async Task ChatListViewModelRetriesAfterInitialLoadFailure()
  {
    var service = new FakeChatService
    {
      FetchMyConversationsError = new InvalidOperationException("list failed"),
      ConversationsResult = new ChatConversationListResponse(
          [
            new ChatConversation(
                "conversation-1",
                "Recovered",
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                "user-1",
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                null,
                null,
                null),
          ],
          new PageInfo(null, false, null)),
    };
    var viewModel = new ChatListViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.FetchMyConversationsError = null;
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("Recovered", Assert.Single(viewModel.Conversations).DisplayTitle);
  }

  [Fact]
  public async Task ChatListViewModelLoadAsyncRefreshesAfterItHasLoaded()
  {
    var service = new FakeChatService
    {
      ConversationsResult = new ChatConversationListResponse(
          [
            new ChatConversation(
                "conversation-1",
                "First title",
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                "user-1",
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                null,
                null,
                null),
          ],
          new PageInfo(null, false, null)),
    };
    var viewModel = new ChatListViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.ConversationsResult = new ChatConversationListResponse(
        [
          new ChatConversation(
              "conversation-1",
              "Updated title",
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              "user-1",
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              null,
              null,
              null),
        ],
        new PageInfo(null, false, null));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Updated title", Assert.Single(viewModel.Conversations).DisplayTitle);
  }

  [Fact]
  public async Task ChatListContinuationDeduplicatesOverlapsAndPreservesCurrentRows()
  {
    var service = new FakeChatService();
    service.FetchMyConversationsAsyncOverride = (after, _, _) => Task.FromResult(
        after is null
            ? new ChatConversationListResponse(
                [Conversation("conversation-1", "Current title")],
                new PageInfo("cursor-1", true, null))
            : new ChatConversationListResponse(
                [
                  Conversation("conversation-2", "Older title"),
                  Conversation("conversation-2", "Duplicate older title"),
                  Conversation("conversation-1", "Stale current title"),
                ],
                new PageInfo(null, false, null)));
    var viewModel = new ChatListViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["conversation-1", "conversation-2"], viewModel.Conversations.Select(row => row.Id));
    Assert.Equal("Current title", viewModel.Conversations[0].Title);
    Assert.Equal("Older title", viewModel.Conversations[1].Title);
  }

  [Fact]
  public async Task SupportThreadsViewModelLoadAsyncRefreshesLoadedRowsWithoutDuplicating()
  {
    var loads = 0;
    var first = new SupportThreadListResponse(
        [
          new SupportThread(
              "thread-1",
              "contact-1",
              "First subject",
              null,
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              null,
              null,
              null,
              null,
              SupportThreadStatus.Open),
        ],
        new PageInfo(null, false, null));
    var second = new SupportThreadListResponse(
        [
          new SupportThread(
              "thread-1",
              "contact-1",
              "Updated subject",
              null,
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              DateTimeOffset.Parse("2026-07-01T10:05:00Z"),
              null,
              null,
              null,
              null,
              SupportThreadStatus.Resolved),
        ],
        new PageInfo(null, false, null));
    var service = new FakeChatService
    {
      FetchSupportThreadsAsyncOverride = (_, _, _) =>
      {
        loads += 1;
        return Task.FromResult(loads == 1 ? first : second);
      },
    };
    var viewModel = new SupportThreadsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal(2, loads);
    var row = Assert.Single(viewModel.Threads);
    Assert.Equal("Updated subject", row.Subject);
    Assert.Equal("resolved", row.ProtocolStatus);
  }

  [Fact]
  public async Task SupportThreadsViewModelRetriesAfterInitialLoadFailure()
  {
    var service = new FakeChatService
    {
      FetchSupportThreadsError = new InvalidOperationException("support failed"),
      SupportThreadsResult = new SupportThreadListResponse(
          [
            new SupportThread(
                "thread-1",
                "contact-1",
                "Recovered support",
                null,
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                null,
                null,
                null,
                null,
                SupportThreadStatus.Open),
          ],
          new PageInfo(null, false, null)),
    };
    var viewModel = new SupportThreadsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.FetchSupportThreadsError = null;
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("Recovered support", Assert.Single(viewModel.Threads).Subject);
  }

  [Fact]
  public async Task SupportThreadsViewModelMergesOverlappingLoadAndCreateById()
  {
    var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var thread = new SupportThread(
        "thread-1",
        "contact-1",
        "Need help",
        null,
        DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
        DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
        null,
        null,
        null,
        null,
        SupportThreadStatus.Open);
    var service = new FakeChatService
    {
      FetchSupportThreadsAsyncOverride = async (_, _, _) =>
      {
        loadStarted.SetResult();
        await releaseLoad.Task.ConfigureAwait(true);
        return new SupportThreadListResponse([thread], new PageInfo(null, false, null));
      },
      CreateSupportThreadResult = new CreateSupportThreadResponse(thread, null),
    };
    var viewModel = new SupportThreadsViewModel(service);

    var loadTask = viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);
    await loadStarted.Task.ConfigureAwait(true);

    var created = await viewModel.CreateThreadAsync("Need help", cancellationToken: TestContext.Current.CancellationToken);

    releaseLoad.SetResult();
    await loadTask.ConfigureAwait(true);

    Assert.NotNull(created);
    Assert.Single(viewModel.Threads);
    Assert.Equal("thread-1", viewModel.Threads[0].Id);
  }

  private static ChatConversation Conversation(string id, string title) =>
      new(
          id,
          title,
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          "user-1",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          null,
          null,
          null);
}
