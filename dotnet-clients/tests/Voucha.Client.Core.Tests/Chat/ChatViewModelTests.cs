using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatViewModelTests
{
  [Fact]
  public async Task ChatConversationViewModelSendsMessageAndGeneratesTitle()
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
                new ChatMessageContent("user", "Hello", null)),
            new ChatMessage(
                "message-2",
                "conversation-1",
                DateTimeOffset.Parse("2026-07-01T10:00:02Z"),
                "assistant-1",
                DateTimeOffset.Parse("2026-07-01T10:00:02Z"),
                null,
                null,
                null,
                new ChatMessageContent("assistant", "Hello there", null)),
          ],
          new PageInfo(null, false, null)),
    };
    service.StreamEvents.Enqueue(new ChatStreamMetadataEvent("conversation-1", "message-1", "message-2", "job-1"));
    service.StreamEvents.Enqueue(new ChatStreamTextEvent("Hello there"));
    service.StreamEvents.Enqueue(new ChatStreamDoneEvent());

    var viewModel = new ChatConversationViewModel(service);

    await viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.Equal("conversation-1", viewModel.ConversationId);
    Assert.Equal("Generated title", viewModel.Title);
    Assert.Empty(viewModel.ToolCalls);
    Assert.Empty(viewModel.SubagentSteps);
    Assert.Empty(viewModel.SubagentTextChunks);
    Assert.False(viewModel.IsStreaming);
    Assert.True(viewModel.CanSend);
    Assert.Equal(2, viewModel.Messages.Count);
    Assert.Equal("Hello", viewModel.Messages[0].Content);
    Assert.Equal("Hello there", viewModel.Messages[1].Content);
    Assert.Equal("/api/v1/conversations/conversation-1/chat", service.LastChatPath);
    Assert.Equal("Hello", service.LastChatMessage);
  }

  [Fact]
  public async Task ChatConversationViewModelKeepsInitialSendErrorsVisible()
  {
    var service = new FakeChatService
    {
      CreateConversationError = new InvalidOperationException("create failed"),
    };
    var viewModel = new ChatConversationViewModel(service);

    await viewModel.SendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ConversationId);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Equal("create failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ChatConversationViewModelRenameRollsBackOnFailure()
  {
    var service = new FakeChatService
    {
      ConversationMessagesResult = new ChatMessagesResponse([], new PageInfo(null, false, null)),
      UpdateConversationTitleError = new InvalidOperationException("rename failed"),
    };
    var viewModel = new ChatConversationViewModel(service);
    await viewModel.LoadAsync("conversation-1", "Original", TestContext.Current.CancellationToken);

    await viewModel.RenameAsync("Updated", TestContext.Current.CancellationToken);

    Assert.Equal("Original", viewModel.Title);
    Assert.Equal("rename failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ChatConversationViewModelLoadReportsMessageFailure()
  {
    var service = new FakeChatService
    {
      FetchConversationMessagesError = new InvalidOperationException("messages failed"),
    };
    var viewModel = new ChatConversationViewModel(service);

    await viewModel.LoadAsync("conversation-1", "Original", TestContext.Current.CancellationToken);

    Assert.Equal("conversation-1", viewModel.ConversationId);
    Assert.Equal("Original", viewModel.Title);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Equal("messages failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ChatConversationViewModelLoadsNewConversationState()
  {
    var viewModel = new ChatConversationViewModel(new FakeChatService());

    await viewModel.LoadAsync(null, cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("New chat", viewModel.DisplayTitle);
    Assert.True(viewModel.CanSend);
    Assert.False(viewModel.CanRename);
    Assert.False(viewModel.CanDelete);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task ChatListViewModelDeleteRollsBackOnFailure()
  {
    var service = new FakeChatService
    {
      ConversationsResult = new ChatConversationListResponse(
          [
            new ChatConversation(
                "conversation-1",
                "Title",
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                "user-1",
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                null,
                null,
                null),
          ],
          new PageInfo(null, false, null)),
      DeleteConversationError = new InvalidOperationException("delete failed"),
    };
    var viewModel = new ChatListViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var row = viewModel.Conversations[0];

    await viewModel.DeleteConversationAsync(row);

    Assert.Single(viewModel.Conversations);
    Assert.Equal("delete failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SupportThreadsViewModelSendsOptionalCreateFields()
  {
    var service = new FakeChatService
    {
      CreateSupportThreadResult = new CreateSupportThreadResponse(
          new SupportThread(
              "thread-1",
              "contact-1",
              "Need help",
              "conversation-1",
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              null,
              null,
              null,
              null,
              SupportThreadStatus.Open),
          null),
    };
    var viewModel = new SupportThreadsViewModel(service);

    var row = await viewModel.CreateThreadAsync(
        "Need help",
        "Initial note",
        "conversation-1",
        TestContext.Current.CancellationToken);

    Assert.NotNull(row);
    Assert.Equal("Need help", service.LastSupportThreadSubject);
    Assert.Equal("Initial note", service.LastSupportThreadMessage);
    Assert.Equal("conversation-1", service.LastSupportThreadConversationId);
  }

  [Fact]
  public async Task SupportThreadsViewModelBlocksDuplicateCreateWhileSubmitting()
  {
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continueCreate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var response = new CreateSupportThreadResponse(
        new SupportThread(
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
            SupportThreadStatus.Open),
        null);
    var service = new FakeChatService
    {
      CreateSupportThreadAsyncOverride = async (_, _) =>
      {
        started.SetResult();
        await continueCreate.Task.ConfigureAwait(true);
        return response;
      },
    };
    var viewModel = new SupportThreadsViewModel(service);

    var firstTask = viewModel.CreateThreadAsync("Need help", cancellationToken: TestContext.Current.CancellationToken);
    await started.Task.ConfigureAwait(true);

    Assert.True(viewModel.IsSubmitting);

    var duplicate = await viewModel.CreateThreadAsync("Need help", cancellationToken: TestContext.Current.CancellationToken);

    Assert.Null(duplicate);
    Assert.Equal(1, service.CreateSupportThreadCount);

    continueCreate.SetResult();
    var row = await firstTask.ConfigureAwait(true);

    Assert.NotNull(row);
    Assert.False(viewModel.IsSubmitting);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Single(viewModel.Threads);
  }

  [Fact]
  public async Task SupportThreadsViewModelLoadsPagesAndResets()
  {
    var service = new FakeChatService
    {
      SupportThreadsResult = new SupportThreadListResponse(
          [
            new SupportThread(
                "thread-1",
                "contact-1",
                "",
                null,
                DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
                DateTimeOffset.Parse("2026-07-01T10:05:00Z"),
                null,
                null,
                null,
                null,
                SupportThreadStatus.Open),
          ],
          new PageInfo(null, true, "cursor-1")),
    };
    var viewModel = new SupportThreadsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.True(viewModel.HasMore);
    Assert.False(viewModel.IsLoading);
    Assert.False(viewModel.HasError);
    Assert.Equal("thread-1", viewModel.Threads[0].Id);
    Assert.Equal("New support thread", viewModel.Threads[0].DisplaySubject);

    viewModel.Reset();

    Assert.Empty(viewModel.Threads);
    Assert.True(viewModel.HasMore);
    Assert.Equal(LoadState.Idle, viewModel.State);
  }

  [Fact]
  public async Task SupportThreadsViewModelReportsLoadFailure()
  {
    var service = new FakeChatService
    {
      FetchSupportThreadsError = new InvalidOperationException("support list failed"),
    };
    var viewModel = new SupportThreadsViewModel(service);

    await viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Equal("support list failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SupportThreadsViewModelRejectsBlankCreateSubject()
  {
    var service = new FakeChatService();
    var viewModel = new SupportThreadsViewModel(service);

    var row = await viewModel.CreateThreadAsync("   ", cancellationToken: TestContext.Current.CancellationToken);

    Assert.Null(row);
    Assert.Null(service.LastSupportThreadSubject);
  }

  [Fact]
  public async Task SupportThreadsViewModelReportsCreateFailure()
  {
    var service = new FakeChatService
    {
      CreateSupportThreadError = new InvalidOperationException("create support failed"),
    };
    var viewModel = new SupportThreadsViewModel(service);

    var row = await viewModel.CreateThreadAsync(
        "Need help",
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.Null(row);
    Assert.Empty(viewModel.Threads);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Equal("create support failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SupportThreadViewModelReportsLoadFailureAndResets()
  {
    var service = new FakeChatService
    {
      FetchSupportThreadError = new InvalidOperationException("support detail failed"),
    };
    var viewModel = new SupportThreadViewModel(service);

    await viewModel.LoadAsync("thread-1", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Equal("support detail failed", viewModel.ErrorMessage);

    viewModel.Reset();

    Assert.Null(viewModel.Thread);
    Assert.Empty(viewModel.Messages);
    Assert.Equal("Support thread", viewModel.Title);
    Assert.Equal(LoadState.Idle, viewModel.State);
  }

  [Fact]
  public void ChatRowsExposeDisplayAndRoleState()
  {
    var conversation = new ChatConversationRow(
        "conversation-1",
        "",
        "New chat",
        DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
        DateTimeOffset.Parse("2026-07-01T10:01:00Z"));
    var userMessage = new ChatMessageRow(
        "message-1",
        "user",
        "Hello",
        DateTimeOffset.Parse("2026-07-01T10:02:00Z"));
    var assistantMessage = new ChatMessageRow(
        "message-2",
        "assistant",
        "Hi",
        DateTimeOffset.Parse("2026-07-01T10:03:00Z"),
        true,
        "tool failed");
    var thread = new SupportThreadRow(
        "thread-1",
        "",
        "New support thread",
        "open",
        "conversation-1",
        DateTimeOffset.Parse("2026-07-01T10:04:00Z"),
        DateTimeOffset.Parse("2026-07-01T10:05:00Z"));
    var supportMessage = new SupportMessageRow(
        "support-message-1",
        "inbound",
        UiTaxonomy.MessageDirection("inbound"),
        "Need help",
        DateTimeOffset.Parse("2026-07-01T10:06:00Z"),
        UiLocalization.English);

    Assert.Equal("New chat", conversation.DisplayTitle);
    Assert.True(userMessage.IsUser);
    Assert.False(userMessage.IsAssistant);
    Assert.True(assistantMessage.IsAssistant);
    Assert.True(assistantMessage.HasError);
    Assert.Equal("tool failed", assistantMessage.Error);
    Assert.Equal("New support thread", thread.DisplaySubject);
    Assert.Equal("open", thread.ProtocolStatus);
    Assert.Equal("Need help", supportMessage.UserContent);
  }

}
