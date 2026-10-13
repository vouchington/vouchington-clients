using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed partial class ChatViewModelProviderTests
{
  [Fact]
  public async Task EditingCompletedMcpDraftAwayAndBackRetriesTheSamePersistedBody()
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
    viewModel.NotifyDraftEdited("Mutate once, edited");
    viewModel.NotifyDraftEdited("Mutate once");
    Assert.False(await viewModel.TrySendAsync("Mutate once", TestContext.Current.CancellationToken));

    Assert.Equal(1, provider.Generations);
    Assert.Equal(2, bodies.Count);
    Assert.Equal(bodies[0], bodies[1]);
  }

  [Fact]
  public async Task DifferentSendCannotReplaceAnUnreconciledCompletedMcpTurn()
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
    viewModel.NotifyDraftEdited("Different request");
    Assert.False(await viewModel.TrySendAsync("Different request", TestContext.Current.CancellationToken));
    Assert.Equal("Retry the previous message before sending a different one.", viewModel.ErrorMessage);
    Assert.Equal(Voucha.Client.Core.Support.LoadState.Error, viewModel.State);
    Assert.Single(bodies);
    Assert.Equal(1, provider.Generations);

    viewModel.NotifyDraftEdited("Mutate once");
    Assert.False(await viewModel.TrySendAsync("Mutate once", TestContext.Current.CancellationToken));
    Assert.Equal(bodies[0], bodies[1]);
    Assert.Equal(1, provider.Generations);
  }
}
