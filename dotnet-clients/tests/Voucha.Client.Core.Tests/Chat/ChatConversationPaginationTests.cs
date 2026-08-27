using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatConversationPaginationTests
{
  [Fact]
  public async Task LoadOlderMessagesAsyncPrependsUniqueOlderRowsAndPreservesCurrentOverlap()
  {
    var service = new FakeChatService
    {
      ConversationMessagesResult = Page([Message("current", "current copy"), Message("newer", "newer")], "older"),
      FetchConversationMessagesPageAsyncOverride = (_, _, _, _) => Task.FromResult(
          Page([Message("oldest", "oldest"), Message("oldest", "duplicate"), Message("current", "stale copy")], null)),
    };
    var viewModel = new ChatConversationViewModel(service);
    await viewModel.LoadAsync("conversation-1", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.LoadOlderMessagesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["oldest", "current", "newer"], viewModel.Messages.Select(message => message.Id));
    Assert.Equal("current copy", viewModel.Messages.Single(message => message.Id == "current").Content);
    Assert.False(viewModel.HasMoreMessages);
  }

  [Fact]
  public async Task LoadOlderMessagesAsyncPreservesRowsAndOffersRetryAfterFailure()
  {
    var attempts = 0;
    var service = new FakeChatService
    {
      ConversationMessagesResult = Page([Message("current", "current")], "older"),
      FetchConversationMessagesPageAsyncOverride = (_, _, _, _) =>
      {
        attempts += 1;
        if (attempts == 1) throw new FormatException("unexpected response shape");
        return Task.FromResult(Page([Message("oldest", "oldest")], null));
      },
    };
    var viewModel = new ChatConversationViewModel(service);
    await viewModel.LoadAsync("conversation-1", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.LoadOlderMessagesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["current"], viewModel.Messages.Select(message => message.Id));
    Assert.Equal("unexpected response shape", viewModel.OlderMessagesErrorMessage);
    Assert.True(viewModel.CanLoadOlderMessages);
    Assert.False(viewModel.ShowLoadOlderMessages);
    Assert.True(viewModel.HasOlderMessagesError);

    await viewModel.LoadOlderMessagesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["oldest", "current"], viewModel.Messages.Select(message => message.Id));
    Assert.Null(viewModel.OlderMessagesErrorMessage);
    Assert.False(viewModel.ShowLoadOlderMessages);
  }

  [Fact]
  public async Task LoadOlderMessagesAsyncIgnoresStalePageAndFinalizerAfterConversationSwitch()
  {
    var pageStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releasePage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService
    {
      FetchConversationMessagesAsyncOverride = (id, _) => Task.FromResult(
          id == "conversation-1"
              ? Page([Message("first", "first")], "older")
              : Page([Message("second", "second")], null)),
      FetchConversationMessagesPageAsyncOverride = async (_, _, _, _) =>
      {
        pageStarted.SetResult();
        await releasePage.Task.ConfigureAwait(true);
        return Page([Message("stale", "stale")], null);
      },
    };
    var viewModel = new ChatConversationViewModel(service);
    await viewModel.LoadAsync("conversation-1", cancellationToken: TestContext.Current.CancellationToken);
    var older = viewModel.LoadOlderMessagesAsync(TestContext.Current.CancellationToken);
    await pageStarted.Task.ConfigureAwait(true);

    await viewModel.LoadAsync("conversation-2", cancellationToken: TestContext.Current.CancellationToken);
    releasePage.SetResult();
    await older.ConfigureAwait(true);

    Assert.Equal("conversation-2", viewModel.ConversationId);
    Assert.Equal(["second"], viewModel.Messages.Select(message => message.Id));
    Assert.False(viewModel.IsLoadingOlderMessages);
    Assert.Null(viewModel.OlderMessagesErrorMessage);
  }

  [Fact]
  public async Task LoadOlderMessagesAsyncCoalescesConcurrentRequests()
  {
    var releasePage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService
    {
      ConversationMessagesResult = Page([Message("current", "current")], "older"),
      FetchConversationMessagesPageAsyncOverride = async (_, _, _, _) =>
      {
        await releasePage.Task.ConfigureAwait(true);
        return Page([Message("oldest", "oldest")], null);
      },
    };
    var viewModel = new ChatConversationViewModel(service);
    await viewModel.LoadAsync("conversation-1", cancellationToken: TestContext.Current.CancellationToken);

    var first = viewModel.LoadOlderMessagesAsync(TestContext.Current.CancellationToken);
    var second = viewModel.LoadOlderMessagesAsync(TestContext.Current.CancellationToken);
    releasePage.SetResult();
    await Task.WhenAll(first, second).ConfigureAwait(true);

    Assert.Equal(1, service.FetchConversationMessagesPageCount);
  }

  private static ChatMessagesResponse Page(IReadOnlyList<ChatMessage> results, string? endCursor) =>
      new(results, new PageInfo(endCursor, endCursor is not null, null));

  private static ChatMessage Message(string id, string content) =>
      new(
          id,
          "conversation-1",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          "user-1",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          null,
          null,
          null,
          new ChatMessageContent("message", content, null));
}
