using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed partial class ChatViewModelProviderTests
{
  [Fact]
  public async Task FreshHistoryAndNextTurnDoNotInheritTemporaryReferralFact()
  {
    ChatConversationViewModel? viewModel = null;
    var attempts = 0;
    CreateClientGeneratedChatBody? persisted = null;
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        var row = Assert.Single(viewModel!.Messages, message => message.Id == body.AssistantMessageId);
        Assert.Equal(attempts == 0, row.HasReferralLinkFacts);
        attempts++;
        persisted = body;
        return Task.FromResult(new ClientGeneratedChatResponse(
            NewChatMessage(body.UserMessageId, "user", body.Message, "2026-07-01T10:00:01Z"),
            NewChatMessage(body.AssistantMessageId, "assistant", body.AssistantContent, "2026-07-01T10:00:02Z"),
            new ClientGeneratedChatTurn(body.UserMessageId, body.AssistantMessageId)));
      },
      FetchConversationMessagesAsyncOverride = (_, _) =>
      {
        var body = persisted!;
        return Task.FromResult(new ChatMessagesResponse(
            [NewChatMessage(body.UserMessageId, "user", body.Message, "2026-07-01T10:00:01Z"),
             NewChatMessage(body.AssistantMessageId, "assistant", body.AssistantContent, "2026-07-01T10:00:02Z")],
            new PageInfo(null, false, null)));
      },
    };
    var provider = new TestMcpLocalChatProvider { ReferralLinkReceipts = [ReferralReceipt("account-a")] };
    viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);
    Assert.True(await viewModel.TrySendAsync("First", TestContext.Current.CancellationToken));
    Assert.Contains(viewModel.Messages, row => row.HasReferralLinkFacts);
    var conversationId = viewModel.ConversationId;
    viewModel.Reset();
    await viewModel.LoadAsync(conversationId, cancellationToken: TestContext.Current.CancellationToken);
    Assert.DoesNotContain(viewModel.Messages, row => row.HasReferralLinkFacts);
    provider.ReferralLinkReceipts = null;
    Assert.True(await viewModel.TrySendAsync("Second", TestContext.Current.CancellationToken));
    Assert.DoesNotContain(viewModel.Messages, row => row.HasReferralLinkFacts);
    Assert.Equal(2, provider.Generations);
  }

  [Theory]
  [InlineData("other-account", false)]
  [InlineData("account-a", true)]
  public async Task WrongAccountAndDuplicateReferralCallsAreNotRendered(string account, bool duplicate)
  {
    ChatConversationViewModel? viewModel = null;
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        Assert.False(Assert.Single(viewModel!.Messages, row => row.Id == body.AssistantMessageId)
            .HasReferralLinkFacts);
        return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Save failed."));
      },
    };
    var receipt = ReferralReceipt(account);
    var provider = new TestMcpLocalChatProvider
    {
      ReferralLinkReceipts = duplicate ? [receipt, receipt] : [receipt],
    };
    viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ReferralFactSurvivesPersistenceOnlyRetryWithoutAnotherGeneration()
  {
    var attempts = 0;
    ChatConversationViewModel? viewModel = null;
    var service = new FakeChatService
    {
      CreateClientGeneratedChatAsyncOverride = (_, body, _) =>
      {
        Assert.Contains("Example Card",
            Assert.Single(viewModel!.Messages, row => row.Id == body.AssistantMessageId).DisplayReferralLinkFacts);
        if (++attempts == 1)
          return Task.FromException<ClientGeneratedChatResponse>(new HttpRequestException("Save failed."));
        return Task.FromResult(new ClientGeneratedChatResponse(
            NewChatMessage(body.UserMessageId, "user", body.Message, "2026-07-01T10:00:01Z"),
            NewChatMessage(body.AssistantMessageId, "assistant", body.AssistantContent, "2026-07-01T10:00:02Z"),
            new ClientGeneratedChatTurn(body.UserMessageId, body.AssistantMessageId)));
      },
    };
    var provider = new TestMcpLocalChatProvider { ReferralLinkReceipts = [ReferralReceipt("account-a")] };
    viewModel = new ChatConversationViewModel(service, new TestChatProviderResolver(provider.Status), provider);
    Assert.False(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Equal(1, provider.Generations);
    Assert.Equal(2, attempts);
  }

  private static McpFeedReferralLinkReceipt ReferralReceipt(string account) =>
      new(account, "call-a", [new McpFeedReferralLink("link-1", "Example Card", new Uri("https://example.test/r/card"))]);
}
