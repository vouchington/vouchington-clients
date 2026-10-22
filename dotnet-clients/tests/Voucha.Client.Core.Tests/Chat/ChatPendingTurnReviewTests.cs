using System.Net.Http;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed partial class ChatViewModelProviderTests
{
  [Fact]
  public async Task PossibleToolSideEffectBlocksUnchangedDraftUntilUserEditsIt()
  {
    var provider = new TestMcpLocalChatProvider
    {
      Failure = new PossibleMcpToolSideEffectException(),
    };
    var locale = new UiLocaleController(new StubDeviceLanguageProvider("fr"));
    var localization = new UiLocalization(locale);
    var viewModel = new ChatConversationViewModel(
        new FakeChatService(), new TestChatProviderResolver(provider.Status), provider,
        localization, locale);

    Assert.False(await viewModel.TrySendAsync("Mutate once", TestContext.Current.CancellationToken));
    Assert.Equal(localization.Localize(UiMessageKey.SharedChatMcpPossibleSideEffect),
        viewModel.ErrorMessage);
    Assert.True(viewModel.PossibleToolSideEffect);
    Assert.Empty(viewModel.Messages);
    Assert.Equal(1, provider.Generations);

    Assert.False(await viewModel.TrySendAsync("Mutate once", TestContext.Current.CancellationToken));
    Assert.Equal(1, provider.Generations);
    viewModel.Reset();
    Assert.False(viewModel.PossibleToolSideEffect);
    Assert.False(await viewModel.TrySendAsync("Mutate once", TestContext.Current.CancellationToken));
    Assert.Equal(2, provider.Generations);
    viewModel.NotifyDraftEdited("Different request");
    Assert.False(viewModel.PossibleToolSideEffect);
  }

  [Fact]
  public async Task AccountChangeBeforePersistenceRetryRequiresContextResetBeforeGeneration()
  {
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
      },
    };
    var provider = new TestMcpLocalChatProvider();
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    provider.UseAccount("account-b");
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(1, provider.Generations);
    Assert.Single(bodies);
    viewModel.Reset();
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(2, provider.Generations);
    Assert.Equal(2, bodies.Count);
    Assert.NotEqual(bodies[0].UserMessageId, bodies[1].UserMessageId);
    Assert.Equal("Reply from account-a", bodies[0].AssistantContent);
    Assert.Equal("Reply from account-b", bodies[1].AssistantContent);
  }

  [Fact]
  public async Task CatalogRefreshRetainsAmbiguousMcpTurnForExactPersistenceRetry()
  {
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
      },
    };
    var provider = new TestMcpLocalChatProvider();
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    provider.RefreshCatalog();
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(1, provider.Generations);
    Assert.Equal(2, bodies.Count);
    Assert.Equal(bodies[0], bodies[1]);
  }

  [Fact]
  public async Task ProviderChangeAfterMcpPersistenceFailureRetainsCompletedTurn()
  {
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
      },
    };
    var provider = new TestMcpLocalChatProvider();
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Mutate once", TestContext.Current.CancellationToken));
    Assert.False(await viewModel.TrySendAsync("Different request", TestContext.Current.CancellationToken));
    Assert.Equal("Retry the previous message before sending a different one.", viewModel.ErrorMessage);
    Assert.Equal(1, provider.Generations);
    Assert.Single(bodies);
    provider.Status = provider.Status with { ModelName = "replacement-model" };
    viewModel.SelectedProviderStatus = provider.Status with
    {
      ModelProvider = "other-local",
      ModelName = "other-model",
    };
    Assert.False(await viewModel.TrySendAsync("Mutate once", TestContext.Current.CancellationToken));

    Assert.Equal(1, provider.Generations);
    Assert.Equal(2, bodies.Count);
    Assert.Equal(bodies[0], bodies[1]);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CompletedMcpTurnRetriesDuringCatalogReloadOrDisconnect(bool disconnected)
  {
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
      },
    };
    var provider = new TestMcpLocalChatProvider();
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    provider.HideCatalog(disconnected);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(1, provider.Generations);
    Assert.Equal(2, bodies.Count);
    Assert.Equal(bodies[0], bodies[1]);
  }

  [Fact]
  public async Task CatalogRefreshDuringAcknowledgedPersistenceKeepsTheCompletedMcpTurn()
  {
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var held = new TaskCompletionSource<ClientGeneratedChatResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var posts = 0;
    var service = new FakeChatService();
    service.CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        posts++;
        service.ConversationMessagesResult = new ChatMessagesResponse(
            [
              NewChatMessage(body.UserMessageId, "user", body.Message, "2026-07-01T10:00:01Z"),
              NewChatMessage(body.AssistantMessageId, "assistant", body.AssistantContent, "2026-07-01T10:00:02Z"),
            ],
            new PageInfo(null, false, null));
        started.TrySetResult();
        return held.Task;
      };
    var provider = new TestMcpLocalChatProvider();
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    var send = viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    provider.RefreshCatalog();
    held.SetResult(new ClientGeneratedChatResponse(
        NewChatMessage("user-1", "user", "Hello", "2026-07-01T10:00:01Z"),
        NewChatMessage("assistant-1", "assistant", "Reply from account-a", "2026-07-01T10:00:02Z"),
        new ClientGeneratedChatTurn("user-1", "assistant-1")));

    Assert.True(await send);
    Assert.Equal(1, provider.Generations);
    Assert.Equal(1, posts);
    Assert.Contains(viewModel.Messages, row => row.Content == "Reply from account-a");
  }

  [Fact]
  public async Task CatalogRefreshJustAfterCompletedGenerationDoesNotRerunTheTurn()
  {
    var service = new FakeChatService();
    var provider = new TestMcpLocalChatProvider();
    provider.AfterGeneration = provider.RefreshCatalog;
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    Assert.True(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Equal(1, provider.Generations);
    Assert.Equal("Reply from account-a", service.LastClientGeneratedChatBody?.AssistantContent);
  }

  [Fact]
  public async Task ChangedSignedInAccountRejectsLaggingMcpSnapshotBeforeGeneration()
  {
    var provider = new TestMcpLocalChatProvider();
    var viewModel = new ChatConversationViewModel(
        new FakeChatService(), new TestChatProviderResolver(provider.Status), provider);
    provider.ChangeSignedInAccount("account-b");

    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Equal(0, provider.Generations);
    Assert.Empty(viewModel.Messages);
  }

  [Fact]
  public async Task StoppedPersistenceMustUnwindBeforeTheSameTurnCanRetry()
  {
    var held = new TaskCompletionSource<ClientGeneratedChatResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        started.TrySetResult();
        return held.Task;
      },
    };
    var provider = AvailableLocalProvider("Local reply");
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    var send = viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    await viewModel.StopStreamingAsync();
    Assert.False(viewModel.CanSend);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Single(bodies);

    held.SetException(new HttpRequestException("Response lost."));
    Assert.False(await send);
    Assert.True(viewModel.CanSend);
    Assert.Empty(viewModel.Messages);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Equal(2, bodies.Count);
    Assert.Equal(bodies[0], bodies[1]);
    Assert.Empty(viewModel.Messages);
  }

  [Fact]
  public async Task EditingDraftAwayAndBackStartsANewLocalTurn()
  {
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
      },
    };
    var generations = 0;
    var provider = AvailableLocalProvider((_, _, _) =>
        Task.FromResult(new LocalChatGenerationResult(
            $"Reply {++generations}", "windows_foundry", "windows-system-language-model")));
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    viewModel.NotifyDraftEdited("Hello!");
    viewModel.NotifyDraftEdited("Hello");
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(2, generations);
    Assert.NotEqual(bodies[0].UserMessageId, bodies[1].UserMessageId);
    Assert.NotEqual(bodies[0].AssistantContent, bodies[1].AssistantContent);
  }

  [Fact]
  public async Task PersistenceRetryDoesNotRequireProviderToRemainAvailable()
  {
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        if (bodies.Count == 1)
        {
          return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
        }
        return Task.FromResult(new ClientGeneratedChatResponse(
            NewChatMessage(body.UserMessageId, "user", body.Message, "2026-07-01T10:00:01Z"),
            NewChatMessage(body.AssistantMessageId, "assistant", body.AssistantContent, "2026-07-01T10:00:02Z"),
            new ClientGeneratedChatTurn(body.UserMessageId, body.AssistantMessageId)));
      },
    };
    var generations = 0;
    var provider = AvailableLocalProvider((_, _, _) =>
        Task.FromResult(new LocalChatGenerationResult(
            $"Reply {++generations}", "windows_foundry", "windows-system-language-model")));
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    viewModel.SelectedProviderStatus = null!; // Picker can clear its selection while ItemsSource refreshes.
    provider.Status = provider.Status with { IsAvailable = false };
    viewModel.SelectedProviderStatus = provider.Status;
    Assert.True(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(1, generations);
    Assert.Equal(2, bodies.Count);
    Assert.Equal(bodies[0], bodies[1]);
  }

  [Fact]
  public async Task ReloadingTheSameConversationRetainsAmbiguousTurnUntilServerReconcilesItsIds()
  {
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
      },
    };
    var generations = 0;
    var provider = AvailableLocalProvider((_, _, _) =>
        Task.FromResult(new LocalChatGenerationResult(
            $"Reply {++generations}", "windows_foundry", "windows-system-language-model")));
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);
    await viewModel.LoadAsync("conversation-1", cancellationToken: TestContext.Current.CancellationToken);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    service.FetchConversationMessagesAsyncOverride = (_, _) =>
        Task.FromException<ChatMessagesResponse>(new HttpRequestException("Refresh failed."));
    await viewModel.LoadAsync("conversation-1", cancellationToken: TestContext.Current.CancellationToken);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Equal(1, generations);
    Assert.Equal(bodies[0], bodies[1]);

    service.FetchConversationMessagesAsyncOverride = (_, _) => Task.FromResult(new ChatMessagesResponse(
        [
          NewChatMessage(bodies[0].UserMessageId, "user", "Hello", "2026-07-01T10:00:01Z"),
          NewChatMessage(bodies[0].AssistantMessageId, "assistant", "Reply 1", "2026-07-01T10:00:02Z"),
        ],
        new PageInfo(null, false, null)));
    await viewModel.LoadAsync("conversation-1", cancellationToken: TestContext.Current.CancellationToken);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(2, generations);
    Assert.NotEqual(bodies[0].UserMessageId, bodies[2].UserMessageId);
  }

  [Fact]
  public async Task ProviderChangeDuringHeldPersistenceCannotDiscardTheSubmittedTurn()
  {
    var held = new TaskCompletionSource<ClientGeneratedChatResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        if (bodies.Count == 1)
        {
          started.SetResult();
          return held.Task;
        }
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
      },
    };
    var generations = 0;
    var provider = AvailableLocalProvider((_, _, _) => Task.FromResult(new LocalChatGenerationResult(
        $"Reply {++generations}", "windows_foundry", "windows-system-language-model")));
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    var send = viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken);
    try
    {
      await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
      var selected = viewModel.SelectedProviderStatus;
      viewModel.SelectedProviderStatus = selected with { ModelProvider = "other-local", ModelName = "other-model" };
      Assert.Equal(selected, viewModel.SelectedProviderStatus);
    }
    finally
    {
      held.SetException(new HttpRequestException("Response lost."));
      await send;
    }

    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Equal(1, generations);
    Assert.Equal(bodies[0], bodies[1]);
  }

  [Fact]
  public async Task WindowsReadinessFailureDoesNotEraseThePendingModelIdentity()
  {
    var bodies = new List<CreateClientGeneratedChatBody>();
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        bodies.Add(body);
        if (bodies.Count == 1)
        {
          return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Response lost."));
        }
        return Task.FromResult(new ClientGeneratedChatResponse(
            NewChatMessage(body.UserMessageId, "user", body.Message, "2026-07-01T10:00:01Z"),
            NewChatMessage(body.AssistantMessageId, "assistant", body.AssistantContent, "2026-07-01T10:00:02Z"),
            new ClientGeneratedChatTurn(body.UserMessageId, body.AssistantMessageId)));
      },
    };
    var generations = 0;
    var provider = AvailableLocalProvider((_, _, _) => Task.FromResult(new LocalChatGenerationResult(
        $"Reply {++generations}", "windows_foundry", "windows-system-language-model")));
    provider.Status = provider.Status with { ModelProvider = LocalChatProviderIds.WindowsSystemLanguageModel };
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    provider.Status = provider.Status with { IsAvailable = false, ModelName = null };
    viewModel.SelectedProviderStatus = provider.Status;
    Assert.True(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(1, generations);
    Assert.Equal(bodies[0], bodies[1]);
  }

  private sealed class TestMcpLocalChatProvider : IMcpLocalChatProvider
  {
    private CancellationTokenSource accountCancellation = new();
    private MemberMcpConnectedSnapshot snapshot;
    private string currentAccount = "account-a";
    private long accountEpoch;
    private bool catalogHidden;

    public TestMcpLocalChatProvider() => snapshot = MakeSnapshot("account-a", 1);

    public int Generations { get; private set; }
    public IReadOnlyList<McpFeedPostReceipt>? FeedPostReceipts { get; set; }
    public Exception? Failure { get; set; }
    public Action? AfterGeneration { get; set; }
    public ChatProviderStatus Status { get; set; } = new(
        ChatProviderKind.Local, "Windows local", true, "Windows system language model",
        "windows_foundry", "windows-system-language-model");
    public string Id => Status.ModelProvider!;

    public void UseAccount(string accountId)
    {
      accountCancellation.Cancel();
      accountCancellation = new();
      currentAccount = accountId;
      accountEpoch++;
      snapshot = MakeSnapshot(accountId, snapshot.Revision + 1);
      catalogHidden = false;
    }
    public void ChangeSignedInAccount(string accountId) => currentAccount = accountId;
    public void RefreshCatalog() => snapshot = MakeSnapshot(snapshot.AccountId, snapshot.Revision + 1);
    public void HideCatalog(bool disconnected)
    {
      catalogHidden = true;
      if (disconnected) accountCancellation.Cancel();
    }
    public MemberMcpConnectedSnapshot? ConnectedMcpSnapshot() => catalogHidden ? null : snapshot;
    public bool MatchesAccount(string accountId) => currentAccount == accountId;
    public bool MatchesAccountEpoch(MemberMcpConnectedSnapshot candidate) =>
        currentAccount == candidate.AccountId && accountEpoch == candidate.AccountEpoch;
    public bool IsCurrent(MemberMcpConnectedSnapshot candidate) =>
        !catalogHidden && candidate.AccountId == snapshot.AccountId && candidate.Revision == snapshot.Revision;

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message, IReadOnlyList<LocalLLMResponseInput> history,
        CancellationToken cancellationToken = default) =>
        GenerateAssistantContentAsync(message, history, snapshot, cancellationToken);

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message, IReadOnlyList<LocalLLMResponseInput> history,
        MemberMcpConnectedSnapshot? generationSnapshot, CancellationToken cancellationToken)
    {
      Generations++;
      if (Failure is not null) return Task.FromException<LocalChatGenerationResult>(Failure);
      AfterGeneration?.Invoke();
      return Task.FromResult(new LocalChatGenerationResult(
          $"Reply from {generationSnapshot?.AccountId}", Status.ModelProvider!, Status.ModelName,
          FeedPostReceipts));
    }

    private MemberMcpConnectedSnapshot MakeSnapshot(string accountId, long revision) =>
        new(accountId, revision, JsonDocument.Parse("{\"tools\":[]}").RootElement.Clone(),
            accountCancellation.Token, accountEpoch);
  }
}
