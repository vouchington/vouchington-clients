using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed partial class ChatViewModelProviderTests
{
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
}
