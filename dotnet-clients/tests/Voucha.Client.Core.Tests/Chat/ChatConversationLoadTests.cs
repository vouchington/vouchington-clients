using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatConversationLoadTests
{
  [Fact]
  public async Task LoadAsyncDisplaysLegacyStringContentMessages()
  {
    var service = new FakeChatService
    {
      ConversationMessagesResult = new ChatMessagesResponse(
          [
            new ChatMessage(
                "message-1",
                "conversation-1",
                DateTimeOffset.Parse("2026-07-01T10:00:01Z"),
                "user-1",
                DateTimeOffset.Parse("2026-07-01T10:00:01Z"),
                null,
                null,
                null,
                new ChatMessageContent("message", "Legacy plain text", null)),
          ],
          new PageInfo(null, false, null)),
    };
    var viewModel = new ChatConversationViewModel(service);

    await viewModel.LoadAsync("conversation-1", "Legacy", TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Messages);
    Assert.Equal("Legacy plain text", row.Content);
  }

  [Fact]
  public async Task LoadAsyncIgnoresStaleConversationMessagesAfterRouteChange()
  {
    var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continueFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService
    {
      FetchConversationMessagesAsyncOverride = async (conversationId, _) =>
      {
        if (conversationId == "conversation-1")
        {
          firstStarted.SetResult();
          await continueFirst.Task.ConfigureAwait(true);
          return new ChatMessagesResponse(
              [
                new ChatMessage(
                    "message-1",
                    "conversation-1",
                    DateTimeOffset.Parse("2026-07-01T10:00:01Z"),
                    "user-1",
                    DateTimeOffset.Parse("2026-07-01T10:00:01Z"),
                    null,
                    null,
                    null,
                    new ChatMessageContent("message", "First conversation", null)),
              ],
              new PageInfo(null, false, null));
        }

        if (conversationId == "conversation-2")
        {
          secondStarted.SetResult();
          return new ChatMessagesResponse(
              [
                new ChatMessage(
                    "message-2",
                    "conversation-2",
                    DateTimeOffset.Parse("2026-07-01T10:00:02Z"),
                    "user-2",
                    DateTimeOffset.Parse("2026-07-01T10:00:02Z"),
                    null,
                    null,
                    null,
                    new ChatMessageContent("message", "Second conversation", null)),
              ],
              new PageInfo(null, false, null));
        }

        throw new InvalidOperationException($"Unexpected conversation {conversationId}");
      },
    };
    var viewModel = new ChatConversationViewModel(service);

    var firstLoad = viewModel.LoadAsync("conversation-1", "First", TestContext.Current.CancellationToken);
    await firstStarted.Task.ConfigureAwait(true);

    var secondLoad = viewModel.LoadAsync("conversation-2", "Second", TestContext.Current.CancellationToken);
    await secondStarted.Task.ConfigureAwait(true);
    await secondLoad.ConfigureAwait(true);

    continueFirst.SetResult();
    await firstLoad.ConfigureAwait(true);

    Assert.Equal("conversation-2", viewModel.ConversationId);
    Assert.Equal("Second", viewModel.Title);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    var row = Assert.Single(viewModel.Messages);
    Assert.Equal("Second conversation", row.Content);
  }
}
