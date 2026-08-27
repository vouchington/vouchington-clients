using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatConversationStreamingTests
{
  [Fact]
  public async Task SendAsyncShowsStreamingAssistantTextAndSidecarsBeforeReload()
  {
    var service = new FakeChatService
    {
      CreateConversationResult = new ChatConversationResponse(new ChatConversation(
          "conversation-1",
          "",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          "user-1",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          null,
          null,
          null)),
      GenerateConversationTitleResult = new ChatConversationResponse(new ChatConversation(
          "conversation-1",
          "Generated title",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          "user-1",
          DateTimeOffset.Parse("2026-07-01T10:01:00Z"),
          "user-1",
          null,
          null)),
      ConversationMessagesResult = new ChatMessagesResponse([], new PageInfo(null, false, null)),
    };
    var readyForInspection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continueStream = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.StreamConversationAsyncOverride = (_, _, _) => StreamAsync();

    async IAsyncEnumerable<ChatStreamEvent> StreamAsync()
    {
      yield return new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1");
      yield return new ChatStreamToolCallEvent("tool-1", "search", "{}");
      yield return new ChatStreamToolResultEvent(
          "tool-1",
          System.Text.Json.JsonDocument.Parse("\"done\"").RootElement.Clone());
      yield return new ChatStreamSubagentStepEvent("helper", "search", "tool-1");
      yield return new ChatStreamSubagentTextEvent("helper", "Working", "tool-1");
      yield return new ChatStreamTextEvent("Hello");
      readyForInspection.SetResult();
      await continueStream.Task.ConfigureAwait(true);
      yield return new ChatStreamDoneEvent();
    }

    var viewModel = new ChatConversationViewModel(service);
    var sendTask = viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    await readyForInspection.Task.ConfigureAwait(true);

    Assert.Equal("conversation-1", viewModel.ConversationId);
    Assert.Equal(2, viewModel.Messages.Count);
    Assert.Equal("user", viewModel.Messages[0].Role);
    Assert.Equal("Hello", viewModel.Messages[0].Content);
    Assert.Equal("assistant", viewModel.Messages[1].Role);
    Assert.Equal("Hello", viewModel.Messages[1].Content);
    Assert.Single(viewModel.ToolCalls);
    Assert.Single(viewModel.ToolResults);
    Assert.Single(viewModel.SubagentSteps);
    Assert.Single(viewModel.SubagentTextChunks);

    continueStream.SetResult();
    await sendTask;

    Assert.Single(viewModel.ToolCalls);
    Assert.Single(viewModel.ToolResults);
    Assert.Single(viewModel.SubagentSteps);
    Assert.Single(viewModel.SubagentTextChunks);
  }

  [Fact]
  public async Task SendAsyncRemovesOptimisticMessageWhenStreamFails()
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
      ConversationMessagesResult = new ChatMessagesResponse([], new PageInfo(null, false, null)),
    };
    service.StreamConversationAsyncOverride = (_, _, _) => ThrowStreamAsync();

    static async IAsyncEnumerable<ChatStreamEvent> ThrowStreamAsync()
    {
      await Task.Yield();
      if (TestContext.Current.CancellationToken.IsCancellationRequested)
      {
        yield return new ChatStreamDoneEvent();
      }
      throw new InvalidOperationException("stream rejected");
    }

    var viewModel = new ChatConversationViewModel(service);

    await viewModel.SendAsync("Rejected", TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Messages);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("stream rejected", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SendAsyncKeepsOptimisticMessageWhenReloadFailsAfterAcceptance()
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
      FetchConversationMessagesError = new InvalidOperationException("reload failed"),
    };
    service.StreamConversationAsyncOverride = (_, _, _) => AcceptedStreamAsync();

    static async IAsyncEnumerable<ChatStreamEvent> AcceptedStreamAsync()
    {
      yield return new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1");
      yield return new ChatStreamTextEvent("Hello");
      yield return new ChatStreamDoneEvent();
      await Task.CompletedTask;
    }

    var viewModel = new ChatConversationViewModel(service);

    await viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("reload failed", viewModel.ErrorMessage);
    Assert.Equal(2, viewModel.Messages.Count);
    Assert.Equal("user", viewModel.Messages[0].Role);
    Assert.Equal("Hello", viewModel.Messages[0].Content);
    Assert.Equal("assistant", viewModel.Messages[1].Role);
    Assert.Equal("Hello", viewModel.Messages[1].Content);
  }

  [Fact]
  public async Task SendAsyncKeepsErrorStateWhenAcceptedStreamEmitsError()
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
    service.StreamEvents.Enqueue(new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1"));
    service.StreamEvents.Enqueue(new ChatStreamErrorEvent("worker timed out"));

    var viewModel = new ChatConversationViewModel(service);

    await viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.False(viewModel.IsStreaming);
    Assert.Equal("worker timed out", viewModel.ErrorMessage);
    Assert.Equal(0, service.FetchConversationMessagesCount);
  }

  [Fact]
  public async Task LoadAsyncDisablesSendWhileLoading()
  {
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continueLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService
    {
      FetchConversationMessagesAsyncOverride = async (_, _) =>
      {
        started.SetResult();
        await continueLoad.Task.ConfigureAwait(true);
        return new ChatMessagesResponse([], new PageInfo(null, false, null));
      },
    };
    var viewModel = new ChatConversationViewModel(service);
    var loadTask = viewModel.LoadAsync("conversation-1", "Existing", TestContext.Current.CancellationToken);

    await started.Task.ConfigureAwait(true);

    Assert.Equal(LoadState.Loading, viewModel.State);
    Assert.False(viewModel.CanSend);

    continueLoad.SetResult();
    await loadTask.ConfigureAwait(true);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.True(viewModel.CanSend);
  }

  [Fact]
  public async Task SendAsyncSkipsPostStreamTitleAndReloadAfterReset()
  {
    var streamReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continueStream = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService
    {
      CreateConversationResult = new ChatConversationResponse(new ChatConversation(
          "conversation-1",
          "",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          "user-1",
          DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
          null,
          null,
          null)),
    };
    service.GenerateConversationTitleAsyncOverride = async (_, _) =>
    {
      await Task.Yield();
      return service.GenerateConversationTitleResult;
    };
    service.StreamConversationAsyncOverride = (_, _, _) => StreamAsync();

    async IAsyncEnumerable<ChatStreamEvent> StreamAsync()
    {
      yield return new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1");
      yield return new ChatStreamTextEvent("Hello");
      streamReady.SetResult();
      await continueStream.Task.ConfigureAwait(true);
      yield return new ChatStreamDoneEvent();
    }

    var viewModel = new ChatConversationViewModel(service);
    var sendTask = viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    await streamReady.Task.ConfigureAwait(true);
    viewModel.Reset();
    continueStream.SetResult();

    await sendTask.ConfigureAwait(true);

    Assert.Equal(0, service.GenerateConversationTitleCount);
    Assert.Equal(0, service.FetchConversationMessagesCount);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.Null(viewModel.ConversationId);
    Assert.Empty(viewModel.Messages);
  }

  [Fact]
  public async Task SendAsyncSkipsCreateResultAfterReset()
  {
    var createStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continueCreate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService();
    service.CreateConversationAsyncOverride = async (_, _) =>
    {
      createStarted.SetResult();
      await continueCreate.Task.ConfigureAwait(true);
      return service.CreateConversationResult;
    };

    var viewModel = new ChatConversationViewModel(service);
    var sendTask = viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    await createStarted.Task.ConfigureAwait(true);
    viewModel.Reset();
    continueCreate.SetResult();
    await sendTask.ConfigureAwait(true);

    Assert.Null(viewModel.ConversationId);
    Assert.Empty(viewModel.Messages);
    Assert.Null(service.LastChatPath);
    Assert.False(viewModel.IsStreaming);
  }

  [Fact]
  public async Task StopStreamingAsyncReturnsLoadedStateAfterReload()
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
      ConversationMessagesResult = new ChatMessagesResponse(
          [
            new ChatMessage(
                "message-3",
                "conversation-1",
                DateTimeOffset.Parse("2026-07-01T10:02:00Z"),
                "user-1",
                DateTimeOffset.Parse("2026-07-01T10:02:00Z"),
                null,
                null,
                null,
                new ChatMessageContent("message", "Persisted after stop", null)),
          ],
          new PageInfo(null, false, null)),
    };
    var streamReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.StreamConversationAsyncOverride = (_, _, cancellationToken) => CancelableStreamAsync(cancellationToken);

    async IAsyncEnumerable<ChatStreamEvent> CancelableStreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
      yield return new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1");
      streamReady.SetResult();
      await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(true);
    }

    var viewModel = new ChatConversationViewModel(service);
    var sendTask = viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    await streamReady.Task.ConfigureAwait(true);
    await viewModel.StopStreamingAsync().ConfigureAwait(true);
    await sendTask.ConfigureAwait(true);

    Assert.False(viewModel.IsStreaming);
    Assert.False(viewModel.IsLoading);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("conversation-1", viewModel.ConversationId);
    Assert.Single(viewModel.Messages);
    Assert.Equal("Persisted after stop", viewModel.Messages[0].Content);
    Assert.Equal(1, service.FetchConversationMessagesCount);
  }

  [Fact]
  public async Task LoadAsyncRetriesAfterSendReloadFailureEvenWithExistingMessages()
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
      FetchConversationMessagesError = new InvalidOperationException("reload failed"),
    };
    service.StreamConversationAsyncOverride = (_, _, _) => AcceptedStreamAsync();

    static async IAsyncEnumerable<ChatStreamEvent> AcceptedStreamAsync()
    {
      yield return new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1");
      yield return new ChatStreamTextEvent("Hello");
      yield return new ChatStreamDoneEvent();
      await Task.CompletedTask;
    }

    var viewModel = new ChatConversationViewModel(service);

    await viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal(2, viewModel.Messages.Count);

    service.FetchConversationMessagesError = null;
    service.ConversationMessagesResult = new ChatMessagesResponse(
        [
          new ChatMessage(
              "message-3",
              "conversation-1",
              DateTimeOffset.Parse("2026-07-01T10:02:00Z"),
              "user-1",
              DateTimeOffset.Parse("2026-07-01T10:02:00Z"),
              null,
              null,
              null,
              new ChatMessageContent("message", "Fresh from server", null)),
        ],
        new PageInfo(null, false, null));

    await viewModel.LoadAsync("conversation-1", "Existing", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Single(viewModel.Messages);
    Assert.Equal("Fresh from server", viewModel.Messages[0].Content);
  }

  [Fact]
  public async Task ResetCancelsActiveStreamingRequest()
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
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.StreamConversationAsyncOverride = (_, _, cancellationToken) => CancelableStreamAsync(cancellationToken);

    async IAsyncEnumerable<ChatStreamEvent> CancelableStreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
      started.SetResult();
      using var registration = cancellationToken.Register(() => cancelled.TrySetResult());
      await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(true);
      yield break;
    }

    var viewModel = new ChatConversationViewModel(service);
    var sendTask = viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    await started.Task.ConfigureAwait(true);
    viewModel.Reset();

    await cancelled.Task.ConfigureAwait(true);
    await sendTask;

    Assert.False(viewModel.IsStreaming);
    Assert.Null(viewModel.ConversationId);
  }

  [Fact]
  public async Task DeleteAsyncCancelsActiveStreamingRequest()
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
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continueStream = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.StreamConversationAsyncOverride = (_, _, cancellationToken) => CancelableStreamAsync(cancellationToken);

    async IAsyncEnumerable<ChatStreamEvent> CancelableStreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
      started.SetResult();
      using var registration = cancellationToken.Register(() => cancelled.TrySetResult());
      yield return new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1");
      await continueStream.Task.ConfigureAwait(true);
      yield return new ChatStreamDoneEvent();
    }

    var viewModel = new ChatConversationViewModel(service);
    var sendTask = viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    await started.Task.ConfigureAwait(true);
    var deleteTask = viewModel.DeleteAsync(TestContext.Current.CancellationToken);

    await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken).ConfigureAwait(true);
    continueStream.SetResult();

    await Task.WhenAll(sendTask, deleteTask).ConfigureAwait(true);

    Assert.False(viewModel.IsStreaming);
    Assert.True(viewModel.IsDeleted);
  }
}
