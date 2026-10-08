using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class DirectMessagesViewModelConcurrencyTests
{
  [Fact]
  public async Task SelectConversationResetsParticipantPolicyBeforeMetadataLoads()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.CompleteConversation(
        "c1",
        new DirectConversationResponse(ConversationWithPolicy("c1", "Thread one", "all_members", "Alice")));
    service.CompleteMessages("c1", new DirectMessagesResponse([], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "u1", "member", "alice")], new PageInfo(null, false, null)));
    await firstLoad;

    Assert.Equal("all_members", viewModel.ParticipantAddPolicy);
    Assert.True(viewModel.CanAddParticipants);

    var secondLoad = viewModel.SelectConversationAsync("c2", TestContext.Current.CancellationToken);

    Assert.Equal("owner_only", viewModel.ParticipantAddPolicy);
    Assert.False(viewModel.CanAddParticipants);

    service.CompleteConversation(
        "c2",
        new DirectConversationResponse(ConversationWithPolicy("c2", "Thread two", "owner_only", "Bob")));
    service.CompleteMessages("c2", new DirectMessagesResponse([], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c2",
        new DirectMessageParticipantsResponse([Participant("p2", "c2", "u2", "member", "bob")], new PageInfo(null, false, null)));
    await secondLoad;
  }

  [Fact]
  public async Task ReloadInboxWaitsForInFlightPageBeforeReplacingRows()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstPage = viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    service.CompleteConversations(new DirectConversationsResponse(
        [Conversation("c1", "Thread one", Time(10, 0, 0), "Alice")],
        new PageInfo("cursor-1", true, null, true)));
    await firstPage;

    var olderPage = viewModel.LoadMoreConversationsAsync(TestContext.Current.CancellationToken);
    Assert.Equal("cursor-1", service.ConversationFetchRequests[1].After);

    var reload = viewModel.ReloadInboxAsync(TestContext.Current.CancellationToken);
    Assert.True(reload.IsCompletedSuccessfully);
    Assert.Equal(["c1"], viewModel.Conversations.Select(row => row.Id));

    service.CompleteConversations(new DirectConversationsResponse(
        [Conversation("old", "Older thread", Time(9, 0, 0), "Old")],
        new PageInfo(null, false, null)));

    for (var attempt = 0; attempt < 20 && service.ConversationFetchRequests.Count < 3; attempt++)
    {
      await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    Assert.Equal(3, service.ConversationFetchRequests.Count);
    Assert.Null(service.ConversationFetchRequests[2].After);
    service.CompleteConversations(new DirectConversationsResponse(
        [Conversation("new", "Newest thread", Time(11, 0, 0), "New")],
        new PageInfo(null, false, null)));
    await olderPage;

    Assert.Equal(["new"], viewModel.Conversations.Select(row => row.Id));
  }

  [Fact]
  public async Task ReloadInboxKeepsExistingRowsWhenFirstPageRefreshFails()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "Thread one", Time(10, 0, 0), "Alice")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    service.FetchDirectMessagesException = new InvalidOperationException("reload failed");

    await viewModel.ReloadInboxAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["c1"], viewModel.Conversations.Select(row => row.Id));
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("reload failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task LoadMoreMessagesIsIgnoredUntilInitialThreadPageLoads()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var loadThread = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    Assert.Single(service.MessageFetchRequests);

    await viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);

    Assert.Single(service.MessageFetchRequests);
    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("m1", "c1", "newest", "alice")], new PageInfo("cursor-1", true, null, true)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    await loadThread;

    var olderPage = viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, service.MessageFetchRequests.Count);
    Assert.Equal("cursor-1", service.MessageFetchRequests[1].After);
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("m0", "c1", "older", "alice")], new PageInfo(null, false, null)));
    await olderPage;
  }

  [Fact]
  public async Task CreateConversationReloadsInboxAfterSendingMessage()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var create = viewModel.CreateConversationAsync(
        ["u1"],
        "hello",
        ["Alice"],
        TestContext.Current.CancellationToken);

    service.CompleteCreateConversation(new DirectConversationResponse(
        Conversation("c1", "Thread one", Time(10, 0, 0), "Alice")));
    await service.WaitForPendingSendRequestsAsync("c1", 1, TestContext.Current.CancellationToken);
    Assert.Single(service.SendRequests);
    service.CompleteSend("c1", new DirectMessageResponse(Message("sent-1", "c1", "hello", "me")));
    await service.WaitForPendingInboxRequestsAsync(1, TestContext.Current.CancellationToken);
    Assert.Single(service.ConversationFetchRequests);
    service.CompleteConversations(new DirectConversationsResponse(
        [Conversation("c1", "Thread one", Time(10, 5, 0), "Alice")],
        new PageInfo(null, false, null)));
    await service.WaitForPendingThreadRequestsAsync("c1", 1, TestContext.Current.CancellationToken);
    Assert.Single(service.MessageFetchRequests);
    Assert.Single(service.DirectConversationFetchRequests);
    Assert.Single(service.ParticipantFetchRequests);
    service.CompleteConversation("c1", new DirectConversationResponse(
        Conversation("c1", "Thread one", Time(10, 5, 0), "Alice")));
    service.CompleteMessages("c1", new DirectMessagesResponse(
        [Message("sent-1", "c1", "hello", "me")],
        new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));

    Assert.True(await create);
    Assert.Single(service.ConversationFetchRequests);
    Assert.Equal(Time(10, 5, 0), Assert.Single(viewModel.Conversations).UpdatedAt);
  }

  private static DirectConversation Conversation(string id, string title, params string[] names) =>
      new(id, "direct", title, Time(), Time(), null, names, "owner_only");

  private static DirectConversation Conversation(
      string id,
      string title,
      DateTimeOffset updatedAt,
      params string[] names) =>
      new(id, "direct", title, Time(), updatedAt, null, names, "owner_only");

  private static DirectConversation ConversationWithPolicy(
      string id,
      string title,
      string participantAddPolicy,
      params string[] names) =>
      new(id, "direct", title, Time(), Time(), null, names, participantAddPolicy);

  private static DirectMessageParticipant Participant(
      string id,
      string conversationId,
      string userId,
      string role,
      string? username) =>
      new(id, conversationId, userId, role, Time(), null, username);

  private static DirectMessage Message(string id, string conversationId, string text, string? username) =>
      new(id, conversationId, text, username, Time(), username);

  private static DateTimeOffset Time() => new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

  private static DateTimeOffset Time(int hour, int minute, int second) =>
      new(2026, 7, 1, hour, minute, second, TimeSpan.Zero);
}
