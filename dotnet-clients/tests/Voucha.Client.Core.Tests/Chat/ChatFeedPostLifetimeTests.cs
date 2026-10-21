using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed partial class ChatViewModelProviderTests
{
  [Fact]
  public async Task AccountRoundTripCannotRetryOldFactOrDispatchAgain()
  {
    var attempts = 0;
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, _, _) =>
      {
        attempts++;
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Save failed."));
      },
    };
    var provider = new TestMcpLocalChatProvider
    {
      FeedPostReceipts = [new McpFeedPostReceipt("account-a", "call-a",
          ["0198ffff-0000-7000-8000-000000000002"])],
    };
    var viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    provider.UseAccount("account-b");
    provider.UseAccount("account-a");
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Equal(1, provider.Generations);
    Assert.Equal(1, attempts);
  }

  [Theory]
  [InlineData("other-account", false)]
  [InlineData("account-a", true)]
  public async Task WrongAccountAndDuplicateCallsAreNotRendered(string account, bool duplicate)
  {
    ChatConversationViewModel? viewModel = null;
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        Assert.False(Assert.Single(viewModel!.Messages, row => row.Id == body.AssistantMessageId)
            .HasFeedPostFacts);
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Save failed."));
      },
    };
    var receipt = new McpFeedPostReceipt(account, "call-a",
        ["0198ffff-0000-7000-8000-000000000002"]);
    var provider = new TestMcpLocalChatProvider
    {
      FeedPostReceipts = duplicate ? [receipt, receipt] : [receipt],
    };
    viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task FeedFactSurvivesPersistenceOnlyRetryWithoutAnotherGeneration()
  {
    var attempts = 0;
    ChatConversationViewModel? viewModel = null;
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        Assert.Contains("0198ffff-0000-7000-8000-000000000002",
            Assert.Single(viewModel!.Messages, row => row.Id == body.AssistantMessageId).DisplayFeedPostFacts);
        if (++attempts == 1)
          return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Save failed."));
        return Task.FromResult(new ClientGeneratedChatResponse(
            NewChatMessage(body.UserMessageId, "user", body.Message, "2026-07-01T10:00:01Z"),
            NewChatMessage(body.AssistantMessageId, "assistant", body.AssistantContent, "2026-07-01T10:00:02Z"),
            new ClientGeneratedChatTurn(body.UserMessageId, body.AssistantMessageId)));
      },
    };
    var provider = new TestMcpLocalChatProvider
    {
      FeedPostReceipts = [new McpFeedPostReceipt("account-a", "call-a",
          ["0198ffff-0000-7000-8000-000000000002"])],
    };
    viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Equal(1, provider.Generations);
    Assert.Equal(2, attempts);
  }
}
