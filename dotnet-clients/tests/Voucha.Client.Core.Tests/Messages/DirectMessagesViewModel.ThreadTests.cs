using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class DirectMessagesViewModelThreadTests
{
  [Fact]
  public async Task SelectConversationLoadsOlderMessagesAndFallbackLabels()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Direct message", "Alice")),
      MessagesResponse = new DirectMessagesResponse(
          [Message("m2", "c1", "newest", "alice")],
          new PageInfo("cursor-1", true, null, true)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me"), Participant("p2", "c1", "u2", "member", null)],
          new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.MessagesResponse = new DirectMessagesResponse(
        [
          Message("m1", "c1", "older", null),
          Message("m1", "c1", "duplicate older", null),
          Message("m2", "c1", "stale newest", null),
        ],
        new PageInfo("cursor-0", false, null, false));

    await viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["m1", "m2"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal(["older", "newest"], viewModel.Messages.Select(row => row.BodyText));
    Assert.Equal("Message", viewModel.Messages[0].SenderLabel);
    Assert.Equal("Member", viewModel.Participants[1].Label);
    Assert.Equal("cursor-1", service.MessageFetchRequests[1].After);
    Assert.False(viewModel.HasMoreMessages);
  }

  [Fact]
  public async Task LoadMoreMessagesIgnoresRequestsBeforeInitialPageCompletes()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var select = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    Assert.Single(service.MessageFetchRequests);

    var loadMore = viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);
    Assert.Single(service.MessageFetchRequests);

    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("m1", "c1", "newest", "alice")], new PageInfo("cursor-1", true, null, true)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    await Task.WhenAll(select, loadMore);

    Assert.Equal(["m1"], viewModel.Messages.Select(row => row.Id));
  }

  [Fact]
  public async Task LoadMoreMessagesIgnoresConcurrentOlderMessageRequests()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("m1", "c1", "newest", "alice")], new PageInfo("cursor-1", true, null, true)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    await firstLoad;

    var loadMore = viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);
    var duplicateLoad = viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, service.MessageFetchRequests.Count);
    service.CompleteMessages("c1", new DirectMessagesResponse([Message("m0", "c1", "older", "alice")], new PageInfo(null, false, null)));
    await Task.WhenAll(loadMore, duplicateLoad);

    Assert.Equal(["m0", "m1"], viewModel.Messages.Select(row => row.Id));
  }

  [Fact]
  public async Task LoadMoreMessagesIgnoresStaleOlderPageAfterReturningToTheSameConversation()
  {
    var service = new QueuedDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("a", TestContext.Current.CancellationToken);
    service.CompleteConversation(0, new DirectConversationResponse(Conversation("a", "Thread A", "Alice")));
    service.CompleteMessages(
        0,
        new DirectMessagesResponse([Message("a-1", "a", "newest-a", "alice")], new PageInfo("cursor-a-1", true, null, true)));
    service.CompleteParticipants(
        0,
        new DirectMessageParticipantsResponse([Participant("a-p1", "a", "me", "owner", "me")], new PageInfo(null, false, null)));
    await firstLoad;

    var olderPage = viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);
    Assert.Equal("cursor-a-1", service.MessageRequests[1].After);

    var switchToB = viewModel.SelectConversationAsync("b", TestContext.Current.CancellationToken);
    service.CompleteConversation(0, new DirectConversationResponse(Conversation("b", "Thread B", "Bob")));
    service.CompleteMessages(
        0,
        new DirectMessagesResponse([Message("b-1", "b", "newest-b", "bob")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        0,
        new DirectMessageParticipantsResponse([Participant("b-p1", "b", "me", "owner", "me")], new PageInfo(null, false, null)));
    await switchToB;

    var returnToA = viewModel.SelectConversationAsync("a", TestContext.Current.CancellationToken);
    service.CompleteConversation(1, new DirectConversationResponse(Conversation("a", "Thread A", "Alice")));
    service.CompleteMessages(
        2,
        new DirectMessagesResponse([Message("a-2", "a", "fresh-a", "alice")], new PageInfo("cursor-a-2", true, null, true)));
    service.CompleteParticipants(
        1,
        new DirectMessageParticipantsResponse([Participant("a-p2", "a", "me", "owner", "me")], new PageInfo(null, false, null)));
    await returnToA;

    service.CompleteMessages(
        1,
        new DirectMessagesResponse([Message("a-old", "a", "stale-a", "alice")], new PageInfo("cursor-a-old", false, null, false)));
    await olderPage;

    var freshOlderPage = viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);

    Assert.Equal("a", viewModel.SelectedConversationId);
    Assert.Equal(["a-2"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal("cursor-a-2", service.MessageRequests[4].After);
    Assert.Equal(LoadState.Loaded, viewModel.ThreadState);

    service.CompleteMessages(
        3,
        new DirectMessagesResponse([Message("a-old-2", "a", "older-a", "alice")], new PageInfo(null, false, null)));
    await freshOlderPage;
  }

  [Fact]
  public async Task SendMessageCompletionDoesNotReloadConversationAfterThreadSwitch()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("m1", "c1", "before", "me")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    await firstLoad;

    var send = viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken);
    var secondLoad = viewModel.SelectConversationAsync("c2", TestContext.Current.CancellationToken);

    service.CompleteConversation("c2", new DirectConversationResponse(Conversation("c2", "Thread two", "Bob")));
    service.CompleteMessages(
        "c2",
        new DirectMessagesResponse([Message("m2", "c2", "new", "bob")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c2",
        new DirectMessageParticipantsResponse([Participant("p2", "c2", "me", "owner", "me")], new PageInfo(null, false, null)));
    await secondLoad;

    service.CompleteSend("c1", new DirectMessageResponse(Message("server-1", "c1", "sent", "me")));
    await service.WaitForPendingInboxRequestsAsync(1, TestContext.Current.CancellationToken);

    service.CompleteConversations(
        new DirectConversationsResponse([
          Conversation("c1", "Thread one", "Alice") with { UpdatedAt = Time(10, 5, 0) },
          Conversation("c2", "Thread two", "Bob"),
        ], new PageInfo(null, false, null)));

    await Task.WhenAll(send, secondLoad);

    Assert.Equal("c2", viewModel.SelectedConversationId);
    Assert.Equal(LoadState.Loaded, viewModel.ThreadState);
    Assert.Equal(["m2"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal(2, service.MessageFetchRequests.Count);
    Assert.Single(service.ConversationFetchRequests);
    Assert.Equal(["c1", "c2"], viewModel.Conversations.Select(row => row.Id));
    Assert.Equal(Time(10, 5, 0), viewModel.Conversations[0].UpdatedAt);
    Assert.Equal("c1", service.SendRequests[0].ConversationId);
    Assert.DoesNotContain(viewModel.Messages, row => row.IsOptimistic);
  }

  [Fact]
  public async Task SendMessageRefreshesInboxAfterThreadSwitchWithoutMutatingTheVisibleThread()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var loadInbox = viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    service.CompleteConversations(new DirectConversationsResponse(
        [Conversation("c1", "Thread one", Time(10, 0, 0), "Alice")],
        new PageInfo(null, false, null)));
    await loadInbox;

    var selectC1 = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("m1", "c1", "before", "me")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    await selectC1;

    var send = viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken);
    var switchToC2 = viewModel.SelectConversationAsync("c2", TestContext.Current.CancellationToken);

    service.CompleteConversation("c2", new DirectConversationResponse(Conversation("c2", "Thread two", "Bob")));
    service.CompleteMessages(
        "c2",
        new DirectMessagesResponse([Message("m2", "c2", "new", "bob")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c2",
        new DirectMessageParticipantsResponse([Participant("p2", "c2", "me", "owner", "me")], new PageInfo(null, false, null)));
    await switchToC2;

    service.CompleteSend("c1", new DirectMessageResponse(Message("server-1", "c1", "sent", "me", Time(10, 5, 0), null)));

    for (var attempt = 0; attempt < 20 && service.ConversationFetchRequests.Count < 2; attempt++)
    {
      await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    service.CompleteConversations(new DirectConversationsResponse(
        [Conversation("c1", "Thread one", Time(10, 5, 0), "Alice"), Conversation("c2", "Thread two", Time(10, 0, 0), "Bob")],
        new PageInfo(null, false, null)));

    await Task.WhenAll(send, switchToC2);

    Assert.Equal("c2", viewModel.SelectedConversationId);
    Assert.Equal(["m2"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal("Alice", viewModel.Conversations[0].Title);
    Assert.Equal(Time(10, 5, 0), viewModel.Conversations[0].UpdatedAt);
    Assert.DoesNotContain(viewModel.Messages, row => row.Id == "server-1");
  }

  [Fact]
  public async Task SendMessageKeepsOptimisticSenderLabelWhenReloadFails()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")),
      MessagesResponse = new DirectMessagesResponse([Message("m1", "c1", "before", "me")], new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me")],
          new PageInfo(null, false, null)),
      SendResponse = new DirectMessageResponse(new DirectMessage("server-1", "c1", "sent", "me", Time(10, 5, 0), null)),
      FetchDirectConversationMessagesException = new InvalidOperationException("reload failed"),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);

    Assert.True(await viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken));

    var row = Assert.Single(viewModel.Messages);
    Assert.Equal("server-1", row.Id);
    Assert.Equal("You", row.SenderLabel);
    Assert.Equal(LoadState.Error, viewModel.ThreadState);
    Assert.Equal("reload failed", viewModel.ThreadErrorMessage);
  }

  [Fact]
  public async Task SendMessageAppendsResponseWhenOptimisticRowIsClearedBeforeCompletion()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("m1", "c1", "before", "me")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    await firstLoad;

    var send = viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken);
    for (var attempt = 0; attempt < 20 && service.SendRequests.Count == 0; attempt++)
    {
      await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    var refresh = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    Assert.Empty(viewModel.Messages);

    service.CompleteSend("c1", new DirectMessageResponse(Message("server-1", "c1", "sent", "me")));

    for (var attempt = 0; attempt < 20 && !viewModel.Messages.Any(row => row.Id == "server-1"); attempt++)
    {
      await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    Assert.Contains(viewModel.Messages, row => row.Id == "server-1");
    await service.WaitForPendingThreadRequestsAsync(
        "c1",
        2,
        TestContext.Current.CancellationToken);

    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse(
            [Message("server-1", "c1", "sent", "me"), Message("m1", "c1", "before", "me")],
            new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse(
            [Message("server-1", "c1", "sent", "me"), Message("m1", "c1", "before", "me")],
            new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    for (var attempt = 0; attempt < 20 && service.ConversationFetchRequests.Count == 0; attempt++)
    {
      await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    service.CompleteConversations(
        new DirectConversationsResponse([Conversation("c1", "Thread one", "Alice")], new PageInfo(null, false, null)));

    await Task.WhenAll(send, refresh);

    Assert.Contains(viewModel.Messages, row => row.Id == "server-1");
  }

  [Fact]
  public async Task SendMessageIgnoresConcurrentDuplicateRequestsForTheSameConversation()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("m1", "c1", "before", "me")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    await firstLoad;

    var send = viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken);
    for (var attempt = 0; attempt < 20 && service.SendRequests.Count == 0; attempt++)
    {
      await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    var duplicateSend = viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken);

    Assert.True(duplicateSend.IsCompletedSuccessfully);
    Assert.False(await duplicateSend);
    Assert.Single(service.SendRequests);

    service.FailSend("c1", new InvalidOperationException("send failed"));
    await send;

    Assert.Equal(LoadState.Error, viewModel.ThreadState);
    Assert.Equal("send failed", viewModel.ThreadErrorMessage);
  }

  [Fact]
  public async Task SendMessageRefreshesInboxRowOrderAfterReloading()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c2", "Thread two", Time(10, 5, 0), "Bob"), Conversation("c1", "Thread one", Time(10, 0, 0), "Alice")],
          new PageInfo(null, false, null)),
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Thread one", Time(10, 10, 0), "Alice")),
      MessagesResponse = new DirectMessagesResponse(
          [Message("m1", "c1", "before", "me")],
          new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me")],
          new PageInfo(null, false, null)),
      SendResponse = new DirectMessageResponse(Message("server-1", "c1", "sent", "me")),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);

    service.ConversationsResponse = new DirectConversationsResponse(
        [Conversation("c1", "Thread one", Time(10, 10, 0), "Alice"), Conversation("c2", "Thread two", Time(10, 5, 0), "Bob")],
        new PageInfo(null, false, null));
    service.MessagesResponse = new DirectMessagesResponse(
        [Message("m2", "c1", "after", "alice"), Message("m1", "c1", "before", "me")],
        new PageInfo(null, false, null));

    Assert.True(await viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken));

    Assert.Equal(["c1", "c2"], viewModel.Conversations.Select(row => row.Id));
    Assert.Equal(Time(10, 10, 0), viewModel.Conversations[0].UpdatedAt);
  }

  private static DirectConversation Conversation(string id, string title, params string[] names) =>
      new(id, "direct", title, Time(), Time(), null, names, "owner_only");

  private static DirectConversation Conversation(
      string id,
      string title,
      DateTimeOffset updatedAt,
      params string[] names) =>
      new(id, "direct", title, Time(), updatedAt, null, names, "owner_only");

  private static DirectMessage Message(
      string id,
      string conversationId,
      string text,
      string? username,
      DateTimeOffset? createdAt = null,
      string? senderUsername = null) =>
      new(id, conversationId, text, username, createdAt ?? Time(), senderUsername ?? username);

  private static DirectMessageParticipant Participant(
      string id,
      string conversationId,
      string userId,
      string role,
      string? username) =>
      new(id, conversationId, userId, role, Time(), null, username);

  private static DateTimeOffset Time() => new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

  private static DateTimeOffset Time(int hour, int minute, int second) =>
      new(2026, 7, 1, hour, minute, second, TimeSpan.Zero);
}
