using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class DirectMessagesViewModelTests
{
  [Fact]
  public async Task LoadInboxMapsConversationTitlesAndPagination()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "Direct message", "Alice", "Bob")],
          new PageInfo("cursor-1", true, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.True(viewModel.HasMoreConversations);
    Assert.Equal("Alice, Bob", Assert.Single(viewModel.Conversations).Title);
    service.ConversationsResponse = new DirectConversationsResponse([], new PageInfo(null, false, null));
    await viewModel.LoadMoreConversationsAsync(TestContext.Current.CancellationToken);

    Assert.Null(service.FetchRequests[0].After);
    Assert.Equal("cursor-1", service.FetchRequests[1].After);
  }

  [Fact]
  public async Task LoadMoreConversationsIgnoresConcurrentInboxPageRequests()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.LoadMoreConversationsAsync(TestContext.Current.CancellationToken);
    var secondLoad = viewModel.LoadMoreConversationsAsync(TestContext.Current.CancellationToken);

    Assert.Single(service.ConversationFetchRequests);
    Assert.True(secondLoad.IsCompletedSuccessfully);

    service.CompleteConversations(new DirectConversationsResponse(
        [Conversation("c1", "Direct message", "Alice")],
        new PageInfo("cursor-1", true, null)));
    await Task.WhenAll(firstLoad, secondLoad);

    Assert.Equal(["c1"], viewModel.Conversations.Select(row => row.Id));
    Assert.True(viewModel.HasMoreConversations);
  }

  [Fact]
  public async Task LoadMoreConversationsDeduplicatesOverlapsAndPreservesCurrentRows()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "Current title", "")],
          new PageInfo("cursor-1", true, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");
    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    service.ConversationsResponse = new DirectConversationsResponse(
        [
          Conversation("c2", "Older title", ""),
          Conversation("c2", "Duplicate older title", ""),
          Conversation("c1", "Stale current title", ""),
        ],
        new PageInfo(null, false, null));

    await viewModel.LoadMoreConversationsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["c1", "c2"], viewModel.Conversations.Select(row => row.Id));
    Assert.Equal("Current title", viewModel.Conversations[0].Title);
    Assert.Equal("Older title", viewModel.Conversations[1].Title);
  }

  [Fact]
  public async Task LoadInboxUsesConversationTitleWhenParticipantNamesAreBlank()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "Fallback title", " ", "")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Fallback title", Assert.Single(viewModel.Conversations).Title);
  }

  [Fact]
  public async Task SelectConversationLoadsThreadAndOwnerParticipantActions()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Direct message", "Alice")),
      MessagesResponse = new DirectMessagesResponse(
          [Message("m1", "c1", "hello", "alice")],
          new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me"), Participant("p2", "c1", "u2", "member", "alice")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.ThreadState);
    Assert.True(viewModel.IsOwner);
    Assert.Equal("hello", Assert.Single(viewModel.Messages).BodyText);
    Assert.False(viewModel.Participants[0].CanRemove);
    Assert.True(viewModel.Participants[1].CanRemove);
    Assert.Equal("Remove", viewModel.Participants[1].RemoveActionLabel);
  }

  [Fact]
  public async Task LoadMoreMessagesDropsStalePageAfterConversationChanges()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("c1-new", "c1", "latest", "alice")], new PageInfo("cursor-1", true, null, true)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    await firstLoad;

    var loadMore = viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);
    var secondLoad = viewModel.SelectConversationAsync("c2", TestContext.Current.CancellationToken);

    service.CompleteConversation("c2", new DirectConversationResponse(Conversation("c2", "Thread two", "Bob")));
    service.CompleteMessages(
        "c2",
        new DirectMessagesResponse([Message("c2-new", "c2", "new", "bob")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c2",
        new DirectMessageParticipantsResponse([Participant("p2", "c2", "me", "owner", "me")], new PageInfo(null, false, null)));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("c1-old", "c1", "old", "alice")], new PageInfo("cursor-0", false, null)));

    await secondLoad;
    await loadMore;

    Assert.Equal("c2", viewModel.SelectedConversationId);
    Assert.Equal(["c2-new"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal("c1", service.MessageFetchRequests[1].ConversationId);
    Assert.Equal("cursor-1", service.MessageFetchRequests[1].After);
  }

  [Fact]
  public async Task CreateConversationReloadsSelectedThreadAfterSendingInitialMessage()
  {
    var service = new RecordingDirectMessagesService
    {
      UserSearchResponse = new UsersSearchResponse(
          [new UserSearchResult("u1", "alice")],
          new PageInfo(null, false, null)),
      ConversationResponse = new DirectConversationResponse(
          new DirectConversation("c1", "direct", "", Time(), Time(), null, null, "owner_only")),
      SendResponse = new DirectMessageResponse(
          new DirectMessage("server-1", "c1", "sent", "me", Time(10, 5, 0), "me")),
      MessagesResponse = new DirectMessagesResponse(
          [Message("m2", "c1", "existing reply", "alice"), Message("m1", "c1", "before", "me")],
          new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me"), Participant("p2", "c1", "u2", "member", "alice")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.SearchUsersAsync(" alice ", TestContext.Current.CancellationToken);
    var created = await viewModel.CreateConversationAsync(
        ["u1"],
        " hi ",
        ["alice"],
        TestContext.Current.CancellationToken);

    Assert.Equal("alice", service.SearchRequests[0].Query);
    Assert.Equal(["u1"], service.CreateRequests[0].UserIds);
    Assert.Equal("hi", service.SendRequests[0].Text);
    Assert.Equal(["c1"], service.ConversationRequests);
    Assert.Single(service.MessageFetchRequests);
    Assert.Single(service.ParticipantRequests);
    Assert.True(created);
    Assert.Equal("alice", Assert.Single(viewModel.Conversations).Title);
    Assert.Equal(["m2", "m1"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal("existing reply", viewModel.Messages[0].BodyText);
    Assert.Equal(2, viewModel.Participants.Count);
    Assert.False(viewModel.Participants[0].CanRemove);
    Assert.True(viewModel.Participants[1].CanRemove);
    Assert.Equal("c1", viewModel.SelectedConversationId);
    Assert.Equal(Time(10, 5, 0), Assert.Single(viewModel.Conversations).UpdatedAt);
  }

  [Fact]
  public async Task CreateConversationReturnsTrueWhenReloadFailsAfterStoringMessage()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(
          new DirectConversation("c1", "direct", "", Time(), Time(), null, null, "owner_only")),
      SendResponse = new DirectMessageResponse(new DirectMessage("server-1", "c1", "sent", null, Time(10, 5, 0), null)),
      FetchDirectConversationMessagesException = new InvalidOperationException("reload failed"),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    Assert.True(await viewModel.CreateConversationAsync(
        ["u1"],
        " hi ",
        cancellationToken: TestContext.Current.CancellationToken));

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal(LoadState.Error, viewModel.ThreadState);
    Assert.Equal("reload failed", viewModel.ThreadErrorMessage);
    Assert.Equal("c1", viewModel.SelectedConversationId);
    Assert.Equal(["server-1"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal("You", Assert.Single(viewModel.Messages).SenderLabel);
  }

  [Fact]
  public async Task CreateConversationResetsPaginationBeforeReloadingNewThread()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(Conversation("c-old", "Old thread", "Alice")),
      ConversationsResponse = new DirectConversationsResponse(
          [new DirectConversation("c1", "direct", "", Time(), Time(), null, ["alice", "bob"], "owner_only")],
          new PageInfo(null, false, null)),
      MessagesResponse = new DirectMessagesResponse(
          [Message("old-2", "c-old", "newest", "alice"), Message("old-1", "c-old", "older", "me")],
          new PageInfo("old-cursor", true, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c-old", "me", "owner", "me")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.SelectConversationAsync("c-old", TestContext.Current.CancellationToken);
    service.ConversationResponse = new DirectConversationResponse(
        new DirectConversation("c1", "direct", "", Time(), Time(), null, null, "owner_only"));
    service.MessagesResponse = new DirectMessagesResponse(
        [Message("m3", "c1", "new thread", "alice")],
        new PageInfo(null, false, null));
    service.ParticipantsResponse = new DirectMessageParticipantsResponse(
        [Participant("p1", "c1", "me", "owner", "me")],
        new PageInfo(null, false, null));

    var created = await viewModel.CreateConversationAsync(
        ["u1"],
        " hi ",
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(created);
    Assert.Equal([null, null], service.MessageFetchRequests.Select(request => request.After));
    Assert.Equal("c1", viewModel.SelectedConversationId);
    Assert.Equal("alice, bob", Assert.Single(viewModel.Conversations).Title);
  }

  [Fact]
  public async Task SendMessageReloadsSelectedThreadAfterSending()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Alice")),
      MessagesResponse = new DirectMessagesResponse(
          [Message("m1", "c1", "before", "me")],
          new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me")],
          new PageInfo(null, false, null)),
      SendResponse = new DirectMessageResponse(Message("server-1", "c1", "sent", "me")),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");
    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.MessagesResponse = new DirectMessagesResponse(
        [Message("m2", "c1", "after", "alice"), Message("m1", "c1", "before", "me")],
        new PageInfo(null, false, null));
    service.ParticipantsResponse = new DirectMessageParticipantsResponse(
        [Participant("p1", "c1", "me", "owner", "me"), Participant("p2", "c1", "u2", "member", "bob")],
        new PageInfo(null, false, null));

    Assert.True(await viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken));

    Assert.Equal("sent", service.SendRequests[0].Text);
    Assert.Equal(2, service.ConversationRequests.Count);
    Assert.Equal(2, service.MessageFetchRequests.Count);
    Assert.Equal(2, service.ParticipantRequests.Count);
    Assert.Equal(["m2", "m1"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal("after", viewModel.Messages[0].BodyText);
    Assert.Equal(2, viewModel.Participants.Count);
    Assert.True(viewModel.Participants[1].CanRemove);
    Assert.DoesNotContain(viewModel.Messages, row => row.IsOptimistic);
  }

  [Fact]
  public async Task SendMessageKeepsCurrentRowsIfReloadFails()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Alice")),
      MessagesResponse = new DirectMessagesResponse(
          [Message("m1", "c1", "before", "me")],
          new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me")],
          new PageInfo(null, false, null)),
      SendResponse = new DirectMessageResponse(Message("server-1", "c1", "sent", "me")),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");
    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.FetchDirectConversationMessagesException = new InvalidOperationException("reload failed");

    Assert.True(await viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken));

    Assert.Equal(LoadState.Error, viewModel.ThreadState);
    Assert.Equal("reload failed", viewModel.ThreadErrorMessage);
    Assert.Equal(["m1", "server-1"], viewModel.Messages.Select(row => row.Id));
    Assert.DoesNotContain(viewModel.Messages, row => row.IsOptimistic);
  }

  [Fact]
  public async Task SelectConversationIgnoresStaleResults()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    var secondLoad = viewModel.SelectConversationAsync("c2", TestContext.Current.CancellationToken);

    service.CompleteConversation("c2", new DirectConversationResponse(Conversation("c2", "Thread two", "Bob")));
    service.CompleteMessages(
        "c2",
        new DirectMessagesResponse([Message("m2", "c2", "new", "bob")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c2",
        new DirectMessageParticipantsResponse([Participant("p2", "c2", "me", "owner", "me")], new PageInfo(null, false, null)));

    await secondLoad;

    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages(
        "c1",
        new DirectMessagesResponse([Message("m1", "c1", "old", "alice")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));

    await firstLoad;

    Assert.Equal("c2", viewModel.SelectedConversationId);
    Assert.Equal(LoadState.Loaded, viewModel.ThreadState);
    Assert.Equal("new", Assert.Single(viewModel.Messages).BodyText);
    Assert.Equal("me", Assert.Single(viewModel.Participants).UserId);
  }

  [Fact]
  public async Task EmptyInputsAndErrorsUpdateDirectMessageStateBranches()
  {
    var service = new RecordingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.SearchUsersAsync("   ", TestContext.Current.CancellationToken);
    Assert.False(await viewModel.CreateConversationAsync(
        [],
        "hi",
        cancellationToken: TestContext.Current.CancellationToken));
    await viewModel.SelectConversationAsync("   ", TestContext.Current.CancellationToken);
    Assert.False(await viewModel.SendMessageAsync("   ", TestContext.Current.CancellationToken));
    await viewModel.AddParticipantAsync("u2", TestContext.Current.CancellationToken);
    await viewModel.RemoveParticipantAsync("u2", TestContext.Current.CancellationToken);
    await viewModel.UpdateParticipantAddPolicyAsync("all_members", TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.ComposerUserResults);
    Assert.Empty(viewModel.ParticipantUserResults);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.Equal(LoadState.Idle, viewModel.ThreadState);
    Assert.Null(viewModel.SelectedConversationId);
    Assert.Empty(service.SearchRequests);
    Assert.Empty(service.CreateRequests);
    Assert.Empty(service.SendRequests);
    Assert.Empty(service.AddRequests);
    Assert.Empty(service.RemoveRequests);
    Assert.Empty(service.PolicyRequests);
  }

  [Fact]
  public async Task FailuresSetErrorStateForSearchCreateLoadThreadAndMutations()
  {
    var searchService = new RecordingDirectMessagesService
    {
      SearchUsersException = new InvalidOperationException("search failed"),
    };
    var searchViewModel = new DirectMessagesViewModel(searchService, "me");

    await searchViewModel.SearchUsersAsync("alice", TestContext.Current.CancellationToken);

    Assert.Empty(searchViewModel.ComposerUserResults);
    Assert.Equal("search failed", searchViewModel.ErrorMessage);

    var loadService = new RecordingDirectMessagesService
    {
      FetchDirectMessagesException = new InvalidOperationException("load failed"),
    };
    var loadViewModel = new DirectMessagesViewModel(loadService, "me");

    await loadViewModel.LoadInboxAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, loadViewModel.State);
    Assert.Equal("load failed", loadViewModel.ErrorMessage);

    var createService = new RecordingDirectMessagesService
    {
      CreateDirectConversationException = new InvalidOperationException("create failed"),
    };
    var createViewModel = new DirectMessagesViewModel(createService, "me");

    Assert.False(await createViewModel.CreateConversationAsync(
        ["u1"],
        "hello",
        cancellationToken: TestContext.Current.CancellationToken));

    Assert.Equal(LoadState.Error, createViewModel.State);
    Assert.Equal("create failed", createViewModel.ErrorMessage);

    var threadService = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Direct message", "Alice")),
      MessagesResponse = new DirectMessagesResponse(
          [Message("m1", "c1", "before", "me")],
          new PageInfo("cursor-1", true, null, true)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me")],
          new PageInfo(null, false, null)),
      SendDirectMessageException = new InvalidOperationException("send failed"),
      AddDirectConversationParticipantException = new InvalidOperationException("add failed"),
      RemoveDirectConversationParticipantException = new InvalidOperationException("remove failed"),
      UpdateDirectConversationParticipantPolicyException = new InvalidOperationException("policy failed"),
    };
    var threadViewModel = new DirectMessagesViewModel(threadService, "me");

    await threadViewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    Assert.False(await threadViewModel.SendMessageAsync("hello", TestContext.Current.CancellationToken));
    await threadViewModel.AddParticipantAsync("u2", TestContext.Current.CancellationToken);
    await threadViewModel.RemoveParticipantAsync("u2", TestContext.Current.CancellationToken);
    await threadViewModel.UpdateParticipantAddPolicyAsync("all_members", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, threadViewModel.ThreadState);
    Assert.Equal("policy failed", threadViewModel.ThreadErrorMessage);
    Assert.Single(threadViewModel.Messages);
    Assert.Equal("before", threadViewModel.Messages[0].BodyText);
    Assert.Equal("c1", threadService.AddRequests[0].ConversationId);
    Assert.Equal("u2", threadService.AddRequests[0].UserId);
    Assert.Equal("c1", threadService.RemoveRequests[0].ConversationId);
    Assert.Equal(("c1", "u2"), threadService.RemoveRequests[0]);
    Assert.Equal("c1", threadService.PolicyRequests[0].ConversationId);
    Assert.Equal("all_members", threadService.PolicyRequests[0].ParticipantAddPolicy);
  }

  private static DirectConversation Conversation(string id, string title, params string[] names) =>
      new(id, "direct", title, Time(), Time(), null, names, "owner_only");

  private static DirectMessage Message(string id, string conversationId, string text, string? username) =>
      new(id, conversationId, text, username, Time(), username);

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
