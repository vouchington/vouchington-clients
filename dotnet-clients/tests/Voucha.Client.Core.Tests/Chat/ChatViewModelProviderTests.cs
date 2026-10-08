using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatViewModelProviderTests
{
  [Fact]
  public async Task UnconfiguredChatDoesNotCreateAConversationOrCallTheRetiredHostedRoute()
  {
    var service = new FakeChatService();
    var viewModel = new ChatConversationViewModel(service);

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.False(accepted);
    Assert.False(viewModel.SelectedProviderStatus.IsAvailable);
    Assert.Equal(0, service.CreateConversationCount);
    Assert.Null(service.LastChatPath);
    Assert.Empty(viewModel.Messages);
  }

  [Fact]
  public async Task ChatConversationViewModelRejectsUnavailableLocalProviderBeforeSending()
  {
    var service = new FakeChatService();
    var localProvider = new TestLocalChatProvider(
        new ChatProviderStatus(
            ChatProviderKind.Local,
            "Windows local",
            false,
            "Windows local chat is unavailable.",
            "windows_foundry",
            "windows-system-language-model"),
        (_, _, _) => Task.FromResult(new LocalChatGenerationResult("unused", "windows_foundry", "windows-system-language-model")));
    var viewModel = new ChatConversationViewModel(
        service,
        new TestChatProviderResolver(localProvider.Status),
        localProvider)
    {
      SelectedProviderStatus = localProvider.Status,
    };

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.False(accepted);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("Windows local chat is unavailable.", viewModel.ErrorMessage);
    Assert.Empty(viewModel.Messages);
    Assert.Equal(0, service.CreateConversationCount);
    Assert.Null(service.LastChatPath);
  }

  [Fact]
  public async Task ChatConversationViewModelHandlesLocalReadinessFailuresBeforeOptimisticInsertion()
  {
    var status = new ChatProviderStatus(ChatProviderKind.Local, "Windows local", true, "Ready.", "windows_foundry", "windows-system-language-model");
    var viewModel = new ChatConversationViewModel(
        new FakeChatService(),
        new TestChatProviderResolver(status),
        new FailingStatusLocalProvider());

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.False(accepted);
    Assert.Equal("Windows system language model operation failed.", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Empty(viewModel.Messages);
  }

  [Fact]
  public async Task ChatConversationViewModelPersistsAvailableLocalProviderTurn()
  {
    var service = new FakeChatService
    {
      ConversationMessagesResult = new ChatMessagesResponse(
          [
            NewChatMessage("message-user", "user", "Hello", "2026-07-01T10:00:01Z"),
            NewChatMessage("message-assistant", "assistant", "Local reply", "2026-07-01T10:00:02Z"),
          ],
          new PageInfo(null, false, null)),
    };
    var localProvider = AvailableLocalProvider("Local reply");
    var viewModel = new ChatConversationViewModel(
        service,
        new TestChatProviderResolver(localProvider.Status),
        localProvider);

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.True(accepted);
    Assert.Equal("/api/v1/conversations/conversation-1/client-generated-chat", service.LastChatPath);
    Assert.Equal("windows_foundry", service.LastClientGeneratedChatBody?.ModelProvider);
    Assert.Equal("windows-system-language-model", service.LastClientGeneratedChatBody?.ModelName);
    Assert.Equal("Hello", service.LastClientGeneratedChatBody?.Message);
    Assert.Equal("Local reply", service.LastClientGeneratedChatBody?.AssistantContent);
    Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", service.LastClientGeneratedChatBody!.UserMessageId);
    Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", service.LastClientGeneratedChatBody.AssistantMessageId);
    Assert.True(string.CompareOrdinal(service.LastClientGeneratedChatBody.UserMessageId, service.LastClientGeneratedChatBody.AssistantMessageId) < 0);
    Assert.Equal(0, service.GenerateConversationTitleCount);
    Assert.Equal(2, viewModel.Messages.Count);
    Assert.Equal("Local reply", viewModel.Messages[1].Content);
    Assert.False(viewModel.IsStreaming);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task WindowsSystemLanguageModelProviderReturnsBackendCompatibleMetadata()
  {
    var provider = new WindowsSystemLanguageModelProvider(new ReadyWindowsSystemLanguageModelRuntime());

    var result = await provider.GenerateAssistantContentAsync(
        "Hello",
        [],
        TestContext.Current.CancellationToken);

    Assert.Equal("Local reply", result.AssistantContent);
    Assert.Equal("windows_foundry", result.ModelProvider);
    Assert.Null(result.ModelName);
  }

  [Fact]
  public async Task ChatConversationViewModelRefreshesLocalProviderStatusBeforeSending()
  {
    var service = new FakeChatService
    {
      ConversationMessagesResult = new ChatMessagesResponse(
          [
            NewChatMessage("message-user", "user", "Hello", "2026-07-01T10:00:01Z"),
            NewChatMessage("message-assistant", "assistant", "Local reply", "2026-07-01T10:00:02Z"),
          ],
          new PageInfo(null, false, null)),
    };
    var unavailable = new ChatProviderStatus(
        ChatProviderKind.Local,
        "Local",
        false,
        "Local model settings are off.",
        "openai_compatible",
        null);
    var localProvider = new TestLocalChatProvider(
        unavailable,
        (_, _, _) => Task.FromResult(new LocalChatGenerationResult("Local reply", "openai_compatible", "local-model")));
    var viewModel = new ChatConversationViewModel(
        service,
        new TestChatProviderResolver(localProvider.Status),
        localProvider)
    {
      SelectedProviderStatus = unavailable,
    };
    localProvider.Status = new(
        ChatProviderKind.Local,
        "Local",
        true,
        "local-model",
        "openai_compatible",
        "local-model");

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.True(accepted);
    Assert.Equal("local-model", viewModel.SelectedProviderStatus.ModelName);
    Assert.Equal("local-model", service.LastClientGeneratedChatBody?.ModelName);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task ChatConversationViewModelPassesPriorMessagesToLocalProvider()
  {
    IReadOnlyList<LocalLLMResponseInput> capturedHistory = [];
    var service = new FakeChatService
    {
      ConversationMessagesResult = new ChatMessagesResponse(
          [
            NewChatMessage("message-user", "user", "Earlier question", "2026-07-01T10:00:01Z"),
            NewChatMessage("message-assistant", "assistant", "Earlier answer", "2026-07-01T10:00:02Z"),
          ],
          new PageInfo(null, false, null)),
    };
    var localProvider = AvailableLocalProvider((_, history, _) =>
    {
      capturedHistory = history;
      return Task.FromResult(new LocalChatGenerationResult("Local reply", "openai_compatible", "actual-local-model"));
    });
    var viewModel = new ChatConversationViewModel(
        service,
        new TestChatProviderResolver(localProvider.Status),
        localProvider);

    await viewModel.LoadAsync("conversation-1", cancellationToken: TestContext.Current.CancellationToken);
    var accepted = await viewModel.TrySendAsync("Now answer", TestContext.Current.CancellationToken);

    Assert.True(accepted);
    Assert.Equal("actual-local-model", service.LastClientGeneratedChatBody?.ModelName);
    Assert.Equal("openai_compatible", service.LastClientGeneratedChatBody?.ModelProvider);
    Assert.Collection(
        capturedHistory,
        item =>
        {
          Assert.Equal("user", item.Role);
          Assert.Equal("Earlier question", item.Content);
        },
        item =>
        {
          Assert.Equal("assistant", item.Role);
          Assert.Equal("Earlier answer", item.Content);
        });
  }

  [Fact]
  public async Task ChatConversationViewModelRemovesLocalMessagesWhenClientGeneratedPersistenceFails()
  {
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, _, _) =>
          Task.FromException<ClientGeneratedChatResponse>(new InvalidOperationException("Persistence failed.")),
    };
    var localProvider = AvailableLocalProvider("Local reply");
    var viewModel = new ChatConversationViewModel(
        service,
        new TestChatProviderResolver(localProvider.Status),
        localProvider);

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.False(accepted);
    Assert.Equal("/api/v1/conversations/conversation-1/client-generated-chat", service.LastChatPath);
    Assert.Equal("Persistence failed.", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Empty(viewModel.Messages);
    Assert.False(viewModel.IsStreaming);
  }

  [Fact]
  public async Task ChatConversationViewModelRestoresTheDraftWhenLocalGenerationFails()
  {
    var service = new FakeChatService();
    var localProvider = AvailableLocalProvider((_, _, _) =>
        Task.FromException<LocalChatGenerationResult>(new InvalidOperationException("Windows system language model operation failed.")));
    var viewModel = new ChatConversationViewModel(
        service,
        new TestChatProviderResolver(localProvider.Status),
        localProvider);

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.False(accepted);
    Assert.Equal("Windows system language model operation failed.", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Empty(viewModel.Messages);
    Assert.Null(service.LastClientGeneratedChatBody);
  }

  [Fact]
  public async Task ChatConversationViewModelCancelsInFlightLocalProviderTurn()
  {
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService();
    var localProvider = AvailableLocalProvider(async (_, _, cancellationToken) =>
    {
      started.SetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(true);
      return new LocalChatGenerationResult("unreachable", "windows_foundry", "windows-system-language-model");
    });
    var viewModel = new ChatConversationViewModel(
        service,
        new TestChatProviderResolver(localProvider.Status),
        localProvider);

    var sendTask = viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);
    await started.Task.ConfigureAwait(true);

    Assert.True(viewModel.IsStreaming);
    Assert.Single(viewModel.Messages);

    await viewModel.StopStreamingAsync().ConfigureAwait(true);
    var accepted = await sendTask.ConfigureAwait(true);

    Assert.False(accepted);
    Assert.False(viewModel.IsStreaming);
    Assert.Empty(viewModel.Messages);
    Assert.Null(service.LastClientGeneratedChatBody);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task ChatConversationViewModelCancelsInFlightLocalPersistence()
  {
    var persistenceStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = async (_, _, cancellationToken) =>
      {
        persistenceStarted.SetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(true);
        throw new InvalidOperationException("unreachable");
      },
    };
    var localProvider = AvailableLocalProvider("Local reply");
    var viewModel = new ChatConversationViewModel(
        service,
        new TestChatProviderResolver(localProvider.Status),
        localProvider);

    var sendTask = viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);
    await persistenceStarted.Task.ConfigureAwait(true);

    Assert.True(viewModel.IsStreaming);
    Assert.Equal(2, viewModel.Messages.Count);

    await viewModel.StopStreamingAsync().ConfigureAwait(true);
    var accepted = await sendTask.ConfigureAwait(true);

    Assert.False(accepted);
    Assert.False(viewModel.IsStreaming);
    Assert.Empty(viewModel.Messages);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task ChatConversationViewModelReportsAcceptedLocalTurnWhenReloadFails()
  {
    var service = new FakeChatService
    {
      FetchConversationMessagesError = new InvalidOperationException("reload failed"),
    };
    var localProvider = AvailableLocalProvider("Local reply");
    var viewModel = new ChatConversationViewModel(
        service,
        new TestChatProviderResolver(localProvider.Status),
        localProvider);

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.True(accepted);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("reload failed", viewModel.ErrorMessage);
    Assert.Equal(2, viewModel.Messages.Count);
    Assert.Equal("Hello", viewModel.Messages[0].Content);
    Assert.Equal("Local reply", viewModel.Messages[1].Content);
  }

  private static TestLocalChatProvider AvailableLocalProvider(string assistantContent) =>
      AvailableLocalProvider((_, _, _) =>
          Task.FromResult(new LocalChatGenerationResult(assistantContent, "windows_foundry", "windows-system-language-model")));

  private static TestLocalChatProvider AvailableLocalProvider(
      Func<string, IReadOnlyList<LocalLLMResponseInput>, CancellationToken, Task<LocalChatGenerationResult>> generateAssistantContentAsync) =>
      new(
          new ChatProviderStatus(
              ChatProviderKind.Local,
              "Windows local",
              true,
              "Windows system language model",
              "windows_foundry",
              "windows-system-language-model"),
          generateAssistantContentAsync);

  private static ChatMessage NewChatMessage(
      string id,
      string role,
      string content,
      string createdAt) =>
      new(
          id,
          "conversation-1",
          DateTimeOffset.Parse(createdAt),
          role == "user" ? "user-1" : "assistant-1",
          DateTimeOffset.Parse(createdAt),
          null,
          null,
          null,
          new ChatMessageContent(role, content, null));

  private sealed class TestChatProviderResolver : IChatProviderResolver
  {
    private readonly ChatProviderStatus localProvider;

    public TestChatProviderResolver(ChatProviderStatus localProvider) => this.localProvider = localProvider;

    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [localProvider];

    public ChatProviderStatus GetDefaultProviderStatus() => localProvider;
  }

  private sealed class TestLocalChatProvider : ILocalChatProvider
  {
    private readonly Func<string, IReadOnlyList<LocalLLMResponseInput>, CancellationToken, Task<LocalChatGenerationResult>> generateAssistantContentAsync;

    public TestLocalChatProvider(
        ChatProviderStatus status,
        Func<string, IReadOnlyList<LocalLLMResponseInput>, CancellationToken, Task<LocalChatGenerationResult>> generateAssistantContentAsync)
    {
      Status = status;
      this.generateAssistantContentAsync = generateAssistantContentAsync;
    }

    public ChatProviderStatus Status { get; set; }

    public string Id => Status.ModelProvider ?? "test-local";

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message,
        IReadOnlyList<LocalLLMResponseInput> history,
        CancellationToken cancellationToken = default) =>
        Status.IsAvailable
            ? generateAssistantContentAsync(message, history, cancellationToken)
            : throw new InvalidOperationException(Status.StatusText);
  }

  private sealed class FailingStatusLocalProvider : ILocalChatProvider
  {
    public string Id => LocalChatProviderIds.WindowsSystemLanguageModel;
    public ChatProviderStatus Status => throw new InvalidOperationException("Windows system language model operation failed.");
    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(string message,
        IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default) =>
        Task.FromException<LocalChatGenerationResult>(new InvalidOperationException("unreachable"));
  }

  private sealed class ReadyWindowsSystemLanguageModelRuntime : IWindowsSystemLanguageModelRuntime
  {
    public WindowsSystemLanguageModelState GetState() =>
        new(WindowsSystemLanguageModelReadiness.Ready);

    public Task EnsureReadyAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string?> GenerateAssistantContentAsync(
        string message,
        IReadOnlyList<LocalLLMResponseInput> history,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>("Local reply");
  }
}
