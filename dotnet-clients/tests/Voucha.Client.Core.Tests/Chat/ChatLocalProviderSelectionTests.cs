using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class ChatLocalProviderSelectionTests
{
  [Fact]
  public async Task RemovedSelectedProviderDoesNotSilentlySendThroughAnotherLocalProvider()
  {
    var fallback = new TestLocalProvider("openai-compatible:actual", true);
    var service = new FakeChatService();
    var viewModel = new ChatConversationViewModel(service, new Resolver(fallback), fallback)
    {
      SelectedProviderStatus = new(ChatProviderKind.Local, "Removed", true, "Ready.", "openai-compatible:missing"),
    };

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.False(accepted);
    Assert.Equal(0, service.CreateConversationCount);
    Assert.Equal(0, fallback.GenerationCalls);
    Assert.Empty(viewModel.Messages);
  }

  [Fact]
  public async Task ProviderRemovedDuringConversationCreationDoesNotUseFallbackOrPersist()
  {
    var creationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseCreation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var selected = new TestLocalProvider("openai-compatible:selected", true);
    var fallback = new TestLocalProvider("openai-compatible:fallback", true);
    var resolver = new Resolver(selected);
    var service = new FakeChatService
    {
      CreateConversationAsyncOverride = async (_, _) =>
      {
        creationStarted.SetResult();
        await releaseCreation.Task;
        return new(new ChatConversation("conversation-1", "", DateTimeOffset.UtcNow, "user-1", DateTimeOffset.UtcNow, null, null, null));
      },
    };
    var viewModel = new ChatConversationViewModel(service, resolver, fallback);
    var send = viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    try
    {
      await creationStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      resolver.IsPresent = false;
      releaseCreation.SetResult();
      Assert.False(await send);
      Assert.Equal(0, selected.GenerationCalls);
      Assert.Equal(0, fallback.GenerationCalls);
      Assert.Null(service.LastClientGeneratedChatBody);
      Assert.Empty(viewModel.Messages);
    }
    finally
    {
      releaseCreation.TrySetResult();
      await send;
    }
  }

  [Fact]
  public async Task SendingRefreshesTheExplicitDynamicProviderInsteadOfTheFallbackProvider()
  {
    var provider = new TestLocalProvider("openai-compatible:profile", true);
    var fallback = new TestLocalProvider("fallback", false);
    var service = new FakeChatService
    {
      ConversationMessagesResult = new([Message("user", "Hello"), Message("assistant", "Local reply")], new PageInfo(null, false, null)),
    };
    var viewModel = new ChatConversationViewModel(service, new Resolver(provider), fallback)
    {
      SelectedProviderStatus = provider.Status,
    };

    var accepted = await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.True(accepted);
    Assert.Equal(1, provider.GenerationCalls);
    Assert.Equal(0, fallback.GenerationCalls);
    Assert.Equal("Local reply", service.LastClientGeneratedChatBody?.AssistantContent);
  }

  [Fact]
  public async Task LocalGenerationReceivesOnlyARecentBoundedHistory()
  {
    var provider = new TestLocalProvider("openai-compatible:profile", true);
    var history = Enumerable.Range(0, 16)
        .Select(index => Message(index % 2 == 0 ? "user" : "assistant", new string((char)('a' + index), 1_000)))
        .ToArray();
    var service = new FakeChatService
    {
      ConversationMessagesResult = new(history, new PageInfo(null, false, null)),
    };
    var viewModel = new ChatConversationViewModel(service, new Resolver(provider), new TestLocalProvider("fallback", false));
    await viewModel.LoadAsync("conversation-1", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);

    Assert.Equal(12, provider.History.Count);
    Assert.Equal(12_000, provider.History.Sum(item => item.Content.Length));
    Assert.DoesNotContain(provider.History, item => item.Content[0] is 'a' or 'b' or 'c' or 'd');
  }

  [Fact]
  public async Task ProviderPickerSelectionPersistsLocalChoice()
  {
    var provider = new TestLocalProvider("openai-compatible:profile", true);
    var resolver = new Resolver(provider);
    var viewModel = new ChatConversationViewModel(new FakeChatService(), resolver, new TestLocalProvider("fallback", false));

    await viewModel.PersistSelectedProviderAsync(TestContext.Current.CancellationToken);
    Assert.Equal(provider.Id, resolver.SavedProviderId);
  }

  [Fact]
  public async Task SuccessfulProviderSelectionRetryClearsThePreviousFailure()
  {
    var provider = new TestLocalProvider("openai-compatible:profile", true);
    var resolver = new Resolver(provider) { SaveError = new IOException("Save failed.") };
    var viewModel = new ChatConversationViewModel(new FakeChatService(), resolver, new TestLocalProvider("fallback", false));

    await viewModel.PersistSelectedProviderAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, viewModel.State);
    resolver.SaveError = null;
    await viewModel.PersistSelectedProviderAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.Equal(provider.Id, resolver.SavedProviderId);
  }

  [Fact]
  public async Task ExplicitWindowsSetupRefreshesTheSelectedProviderStatus()
  {
    var runtime = new SetupRuntime();
    var provider = new WindowsSystemLanguageModelProvider(runtime);
    var viewModel = new ChatConversationViewModel(new FakeChatService(), new Resolver(provider), new TestLocalProvider("fallback", false));

    Assert.True(viewModel.CanSetUpSelectedWindowsSystemLanguageModel);
    await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(1, runtime.EnsureCalls);
    Assert.True(viewModel.SelectedProviderStatus.IsAvailable);
    Assert.False(viewModel.CanSetUpSelectedWindowsSystemLanguageModel);
  }

  [Fact]
  public async Task ProviderRefreshIgnoresTransientPickerNullAndReselectsFromTheNewList()
  {
    var provider = new WindowsSystemLanguageModelProvider(new SetupRuntime());
    var viewModel = new ChatConversationViewModel(new FakeChatService(), new Resolver(provider), new TestLocalProvider("fallback", false));
    var notifications = new List<string?>();
    ChatProviderStatus? selectedWhileListChanged = null;
    viewModel.PropertyChanged += (_, args) =>
    {
      notifications.Add(args.PropertyName);
      if (args.PropertyName == nameof(ChatConversationViewModel.ProviderStatuses))
      {
        viewModel.SelectedProviderStatus = null!; // The bound Picker temporarily has no selected item.
        selectedWhileListChanged = viewModel.SelectedProviderStatus;
      }
    };

    await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.NotNull(selectedWhileListChanged);
    Assert.Equal(LocalChatProviderIds.WindowsSystemLanguageModel, selectedWhileListChanged.ModelProvider);
    Assert.True(viewModel.SelectedProviderStatus.IsAvailable);
    Assert.Contains(viewModel.SelectedProviderStatus, viewModel.ProviderStatuses);
    Assert.True(notifications.IndexOf(nameof(ChatConversationViewModel.ProviderStatuses)) <
        notifications.LastIndexOf(nameof(ChatConversationViewModel.SelectedProviderStatus)));
  }

  [Theory]
  [InlineData(WindowsSystemLanguageModelReadiness.Unsupported)]
  [InlineData(WindowsSystemLanguageModelReadiness.Disabled)]
  [InlineData(WindowsSystemLanguageModelReadiness.Ready)]
  public void WindowsSetupIsOnlyAvailableWhenTheModelIsNotReady(WindowsSystemLanguageModelReadiness readiness)
  {
    var provider = new WindowsSystemLanguageModelProvider(new SetupRuntime(readiness));
    var viewModel = new ChatConversationViewModel(new FakeChatService(), new Resolver(provider), new TestLocalProvider("fallback", false));

    Assert.False(viewModel.CanSetUpSelectedWindowsSystemLanguageModel);
  }

  [Fact]
  public async Task NativeReadinessFailureProducesAnUnavailableStatusDuringConstructionAndRefresh()
  {
    var provider = new WindowsSystemLanguageModelProvider(new ThrowingStateRuntime());
    var viewModel = new ChatConversationViewModel(new FakeChatService(), new Resolver(provider), new TestLocalProvider("fallback", false));

    Assert.False(viewModel.SelectedProviderStatus.IsAvailable);
    Assert.Equal(LocalChatProviderIds.WindowsSystemLanguageModel, viewModel.SelectedProviderStatus.ModelProvider);
    Assert.False(viewModel.CanSetUpSelectedWindowsSystemLanguageModel);

    await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(viewModel.SelectedProviderStatus.IsAvailable);
  }

  [Fact]
  public async Task WindowsSetupFailureIsPresentedThroughConversationState()
  {
    var provider = new WindowsSystemLanguageModelProvider(new SetupRuntime(ensure: _ => throw new InvalidOperationException("Setup failed.")));
    var viewModel = new ChatConversationViewModel(new FakeChatService(), new Resolver(provider), new TestLocalProvider("fallback", false));

    await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal("Setup failed.", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.CanSetUpSelectedWindowsSystemLanguageModel);
  }

  [Fact]
  public async Task CancelledWindowsSetupDoesNotPresentAnError()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var provider = new WindowsSystemLanguageModelProvider(new SetupRuntime(ensure: token => Task.FromCanceled(token)));
    var viewModel = new ChatConversationViewModel(new FakeChatService(), new Resolver(provider), new TestLocalProvider("fallback", false));

    await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync(cancellationToken: cancellation.Token);

    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.True(viewModel.CanSetUpSelectedWindowsSystemLanguageModel);
  }

  [Fact]
  public async Task NativeCancelledWindowsSetupDoesNotEscapeThePageOperation()
  {
    var provider = new WindowsSystemLanguageModelProvider(new SetupRuntime(ensure: _ => Task.FromException(new OperationCanceledException())));
    var viewModel = new ChatConversationViewModel(new FakeChatService(), new Resolver(provider), new TestLocalProvider("fallback", false));

    await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(LoadState.Idle, viewModel.State);
  }

  [Fact]
  public async Task SuccessfulWindowsSetupRetryClearsThePreviousFailure()
  {
    var provider = new WindowsSystemLanguageModelProvider(new RetrySetupRuntime());
    var viewModel = new ChatConversationViewModel(new FakeChatService(), new Resolver(provider), new TestLocalProvider("fallback", false));

    await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, viewModel.State);
    await viewModel.SetUpSelectedWindowsSystemLanguageModelAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.True(viewModel.SelectedProviderStatus.IsAvailable);
  }

  private static ChatMessage Message(string role, string content) => new(
      Guid.NewGuid().ToString("N"), "conversation-1", DateTimeOffset.UtcNow, "user-1", DateTimeOffset.UtcNow,
      null, null, null, new ChatMessageContent(role, content, null));

  private sealed class Resolver(ILocalChatProvider provider) : IChatProviderResolver, ILocalChatProviderResolver, IChatProviderSelectionStore
  {
    public bool IsPresent { get; set; } = true;
    public string? SavedProviderId { get; private set; }
    public Exception? SaveError { get; set; }
    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [provider.Status];
    public ChatProviderStatus GetDefaultProviderStatus() => provider.Status;
    public ILocalChatProvider? GetLocalProvider(string providerId) => IsPresent && providerId == provider.Id ? provider : null;
    public void SaveSelectedProvider(string? providerId)
    {
      if (SaveError is not null) throw SaveError;
      SavedProviderId = providerId;
    }
  }

  private sealed class TestLocalProvider(string id, bool available) : ILocalChatProvider
  {
    public int GenerationCalls { get; private set; }
    public IReadOnlyList<LocalLLMResponseInput> History { get; private set; } = [];
    public string Id => id;
    public ChatProviderStatus Status => new(ChatProviderKind.Local, id, available, available ? "Ready." : "Unavailable.", id, available ? "model" : null);
    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(string message, IReadOnlyList<LocalLLMResponseInput> history,
        CancellationToken cancellationToken = default)
    {
      GenerationCalls++;
      History = history;
      return available ? Task.FromResult(new LocalChatGenerationResult("Local reply", "openai_compatible", "model")) : Task.FromException<LocalChatGenerationResult>(new InvalidOperationException("Unavailable."));
    }
  }

  private sealed class SetupRuntime(
      WindowsSystemLanguageModelReadiness initialReadiness = WindowsSystemLanguageModelReadiness.NotReady,
      Func<CancellationToken, Task>? ensure = null) : IWindowsSystemLanguageModelRuntime
  {
    private WindowsSystemLanguageModelReadiness readiness = initialReadiness;
    public int EnsureCalls { get; private set; }
    public WindowsSystemLanguageModelState GetState() => new(readiness);
    public async Task EnsureReadyAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
      EnsureCalls++;
      if (ensure is not null) await ensure(cancellationToken);
      readiness = WindowsSystemLanguageModelReadiness.Ready;
      progress?.Report(1);
    }
    public Task<string?> GenerateAssistantContentAsync(string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default) => Task.FromResult<string?>("Local reply");
  }

  private sealed class RetrySetupRuntime : IWindowsSystemLanguageModelRuntime
  {
    private bool ready;
    private int attempts;
    public WindowsSystemLanguageModelState GetState() => new(ready ? WindowsSystemLanguageModelReadiness.Ready : WindowsSystemLanguageModelReadiness.NotReady);
    public Task EnsureReadyAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
      if (++attempts == 1) throw new InvalidOperationException("Setup failed.");
      ready = true;
      return Task.CompletedTask;
    }
    public Task<string?> GenerateAssistantContentAsync(string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default) => Task.FromResult<string?>("Local reply");
  }

  private sealed class ThrowingStateRuntime : IWindowsSystemLanguageModelRuntime
  {
    public WindowsSystemLanguageModelState GetState() => throw new InvalidOperationException("Native readiness failed.");
    public Task EnsureReadyAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<string?> GenerateAssistantContentAsync(string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default) => Task.FromResult<string?>("Local reply");
  }
}
