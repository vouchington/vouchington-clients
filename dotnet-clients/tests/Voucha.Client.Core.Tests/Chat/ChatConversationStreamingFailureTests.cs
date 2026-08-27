using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatConversationStreamingFailureTests
{
  [Fact]
  public async Task SendAsyncRemovesExistingConversationMessageWhenStreamFailsBeforeMetadata()
  {
    var service = new FakeChatService
    {
      ConversationMessagesResult = new ChatMessagesResponse([], new PageInfo(null, false, null)),
    };
    service.StreamConversationAsyncOverride = (_, _, _) => ThrowStreamAsync();

    static async IAsyncEnumerable<ChatStreamEvent> ThrowStreamAsync()
    {
      await Task.Yield();
      if (DateTimeOffset.UtcNow == DateTimeOffset.MinValue) yield return new ChatStreamDoneEvent();
      throw new InvalidOperationException("stream rejected");
    }

    var viewModel = new ChatConversationViewModel(service);
    await viewModel.LoadAsync("conversation-1", "Existing", TestContext.Current.CancellationToken);

    await viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Messages);
    Assert.Equal(LoadState.Error, viewModel.State);
  }

  [Fact]
  public async Task SendAsyncIgnoresStreamFailuresAfterRouteReset()
  {
    var streamReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continueStream = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService
    {
      FetchConversationMessagesAsyncOverride = (conversationId, _) =>
      {
        if (conversationId == "conversation-1")
        {
          return Task.FromResult(
              new ChatMessagesResponse(
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
                  new PageInfo(null, false, null)));
        }

        if (conversationId == "conversation-2")
        {
          return Task.FromResult(
              new ChatMessagesResponse(
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
                  new PageInfo(null, false, null)));
        }

        throw new InvalidOperationException($"Unexpected conversation {conversationId}");
      },
    };
    service.StreamConversationAsyncOverride = (_, _, _) => ThrowStreamAsync();

    async IAsyncEnumerable<ChatStreamEvent> ThrowStreamAsync()
    {
      streamReady.SetResult();
      await continueStream.Task.ConfigureAwait(true);
      if (DateTimeOffset.UtcNow == DateTimeOffset.MinValue) yield return new ChatStreamDoneEvent();
      throw new InvalidOperationException("stream rejected");
    }

    var viewModel = new ChatConversationViewModel(service);
    await viewModel.LoadAsync("conversation-1", "First", TestContext.Current.CancellationToken);

    var sendTask = viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);
    await streamReady.Task.ConfigureAwait(true);

    await viewModel.LoadAsync("conversation-2", "Second", TestContext.Current.CancellationToken);
    continueStream.SetResult();
    await sendTask.ConfigureAwait(true);

    Assert.Equal("conversation-2", viewModel.ConversationId);
    Assert.Equal("Second", viewModel.Title);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
    var row = Assert.Single(viewModel.Messages);
    Assert.Equal("Second conversation", row.Content);
  }

  [Fact]
  public async Task SendAsyncKeepsOptimisticMessagesWhenAcceptedStreamThrows()
  {
    var service = new FakeChatService
    {
      CreateConversationResult = new ChatConversationResponse(new ChatConversation(
          "conversation-1",
          "Existing",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          "user-1",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          null,
          null,
          null)),
    };
    service.StreamConversationAsyncOverride = (_, _, _) => AcceptedThenThrowStreamAsync();

    var viewModel = new ChatConversationViewModel(service);

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.True(accepted);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("stream dropped", viewModel.ErrorMessage);
    Assert.Equal(2, viewModel.Messages.Count);
    Assert.Equal("user", viewModel.Messages[0].Role);
    Assert.Equal("Hello", viewModel.Messages[0].Content);
    Assert.Equal("assistant", viewModel.Messages[1].Role);
    Assert.Equal(string.Empty, viewModel.Messages[1].Content);
    Assert.True(viewModel.Messages[1].HasError);
    Assert.Equal("stream dropped", viewModel.Messages[1].Error);
  }

  [Fact]
  public async Task SendAsyncMarksPartialAssistantFailedWhenAcceptedStreamIsIncomplete()
  {
    var service = new FakeChatService
    {
      CreateConversationResult = new ChatConversationResponse(new ChatConversation(
          "conversation-1",
          "Existing",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          "user-1",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          null,
          null,
          null)),
    };
    service.StreamConversationAsyncOverride = (_, _, _) => IncompletePartialStreamAsync();

    var viewModel = new ChatConversationViewModel(service);

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.True(accepted);
    Assert.Equal(LoadState.Error, viewModel.State);
    var assistant = Assert.Single(viewModel.Messages, row => row.IsAssistant);
    Assert.Equal("Partial answer", assistant.Content);
    Assert.True(assistant.HasError);
    Assert.Equal("The response was interrupted. Please try again.", assistant.Error);
  }

  [Theory]
  [InlineData("Partial answer")]
  [InlineData("")]
  public async Task SendAsyncMarksAssistantFailedForNamedStreamError(string partialContent)
  {
    var service = new FakeChatService
    {
      CreateConversationResult = new ChatConversationResponse(new ChatConversation(
          "conversation-1", "Existing", DateTimeOffset.UtcNow, "user-1",
          DateTimeOffset.UtcNow, null, null, null)),
    };
    service.StreamConversationAsyncOverride = (_, _, _) => NamedErrorStreamAsync(partialContent);
    var viewModel = new ChatConversationViewModel(service);

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.True(accepted);
    var assistant = Assert.Single(viewModel.Messages, row => row.IsAssistant);
    Assert.Equal(partialContent, assistant.Content);
    Assert.True(assistant.HasError);
    Assert.Equal("Please retry this response.", assistant.Error);
  }

  private static async IAsyncEnumerable<ChatStreamEvent> AcceptedThenThrowStreamAsync()
  {
    yield return new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1");
    await Task.Yield();
    throw new InvalidOperationException("stream dropped");
  }

  private static async IAsyncEnumerable<ChatStreamEvent> IncompletePartialStreamAsync()
  {
    yield return new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1");
    yield return new ChatStreamTextEvent("Partial answer");
    await Task.Yield();
    throw new ChatStreamIncompleteException();
  }

  private static async IAsyncEnumerable<ChatStreamEvent> NamedErrorStreamAsync(string content)
  {
    yield return new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1");
    if (content.Length > 0) yield return new ChatStreamTextEvent(content);
    yield return new ChatStreamErrorEvent("Please retry this response.");
    await Task.Yield();
  }
}
