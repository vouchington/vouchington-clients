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
    provider.Status = provider.Status with { IsAvailable = false };
    viewModel.SelectedProviderStatus = provider.Status;
    Assert.True(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(1, generations);
    Assert.Equal(2, bodies.Count);
    Assert.Equal(bodies[0], bodies[1]);
  }
}
