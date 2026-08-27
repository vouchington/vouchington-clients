using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class DirectMessagesViewModelRequestTokenTests
{
  [Fact]
  public async Task SelectConversationIgnoresStaleLoadAfterReturningToTheSameThread()
  {
    var service = new QueuedDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("a", TestContext.Current.CancellationToken);
    var secondLoad = viewModel.SelectConversationAsync("b", TestContext.Current.CancellationToken);
    var thirdLoad = viewModel.SelectConversationAsync("a", TestContext.Current.CancellationToken);

    service.CompleteConversation(0, new DirectConversationResponse(Conversation("b", "Thread B", "Bob")));
    service.CompleteMessages(0, new DirectMessagesResponse([Message("b-1", "b", "from-b", "bob")], new PageInfo(null, false, null)));
    service.CompleteParticipants(0, new DirectMessageParticipantsResponse([Participant("b-p1", "b", "me", "owner", "me")], new PageInfo(null, false, null)));

    service.CompleteConversation(1, new DirectConversationResponse(Conversation("a", "Thread A", "Alice")));
    service.CompleteMessages(1, new DirectMessagesResponse([Message("a-2", "a", "fresh-a", "alice")], new PageInfo(null, false, null)));
    service.CompleteParticipants(1, new DirectMessageParticipantsResponse([Participant("a-p2", "a", "me", "owner", "me")], new PageInfo(null, false, null)));

    await Task.WhenAll(secondLoad, thirdLoad);

    Assert.Equal("a", viewModel.SelectedConversationId);
    Assert.Equal(["a-2"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal(["a-p2"], viewModel.Participants.Select(row => row.Id));

    service.CompleteConversation(0, new DirectConversationResponse(Conversation("a", "Thread A", "Alice")));
    service.CompleteMessages(0, new DirectMessagesResponse([Message("a-1", "a", "stale-a", "alice")], new PageInfo(null, false, null)));
    service.CompleteParticipants(0, new DirectMessageParticipantsResponse([Participant("a-p1", "a", "me", "owner", "me")], new PageInfo(null, false, null)));

    await firstLoad;

    Assert.Equal("a", viewModel.SelectedConversationId);
    Assert.Equal(["a-2"], viewModel.Messages.Select(row => row.Id));
    Assert.Equal(["a-p2"], viewModel.Participants.Select(row => row.Id));
    Assert.Equal(LoadState.Loaded, viewModel.ThreadState);
  }

  [Fact]
  public async Task LoadMoreMessagesDoesNotStayBlockedAfterSwitchingThreads()
  {
    var service = new QueuedDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var loadA = viewModel.SelectConversationAsync("a", TestContext.Current.CancellationToken);
    service.CompleteConversation(0, new DirectConversationResponse(Conversation("a", "Thread A", "Alice")));
    service.CompleteMessages(
        0,
        new DirectMessagesResponse([Message("a-1", "a", "newest-a", "alice")], new PageInfo("cursor-a-1", true, null, true)));
    service.CompleteParticipants(0, new DirectMessageParticipantsResponse([Participant("a-p1", "a", "me", "owner", "me")], new PageInfo(null, false, null)));
    await loadA;

    var olderA = viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);
    Assert.False(olderA.IsCompletedSuccessfully);
    Assert.Equal("a", service.MessageRequests[1].ConversationId);
    Assert.Equal("cursor-a-1", service.MessageRequests[1].After);

    var loadB = viewModel.SelectConversationAsync("b", TestContext.Current.CancellationToken);
    service.CompleteConversation(0, new DirectConversationResponse(Conversation("b", "Thread B", "Bob")));
    service.CompleteMessages(
        0,
        new DirectMessagesResponse([Message("b-1", "b", "newest-b", "bob")], new PageInfo("cursor-b-1", true, null, true)));
    service.CompleteParticipants(0, new DirectMessageParticipantsResponse([Participant("b-p1", "b", "me", "owner", "me")], new PageInfo(null, false, null)));
    await loadB;

    var olderB = viewModel.LoadMoreMessagesAsync(TestContext.Current.CancellationToken);

    Assert.False(olderB.IsCompletedSuccessfully);
    Assert.Equal("b", service.MessageRequests[3].ConversationId);
    Assert.Equal("cursor-b-1", service.MessageRequests[3].After);

    service.CompleteMessages(
        1,
        new DirectMessagesResponse([Message("b-0", "b", "older-b", "bob")], new PageInfo(null, false, null)));
    service.CompleteMessages(
        1,
        new DirectMessagesResponse([Message("a-0", "a", "older-a", "alice")], new PageInfo(null, false, null)));

    await Task.WhenAll(olderA, olderB);

    Assert.Equal("b", viewModel.SelectedConversationId);
    Assert.Equal(["b-0", "b-1"], viewModel.Messages.Select(row => row.Id));
  }

  [Fact]
  public async Task ParticipantSearchResultsAreInvalidatedWhenChangingThreads()
  {
    var service = new QueuedDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    await LoadThreadAsync(viewModel, service, 0, "a", "Thread A", "Alice");

    var firstSearch = viewModel.SearchParticipantUsersAsync("al", TestContext.Current.CancellationToken);
    var switchThread = viewModel.SelectConversationAsync("b", TestContext.Current.CancellationToken);
    service.CompleteConversation(0, new DirectConversationResponse(Conversation("b", "Thread B", "Bob")));
    service.CompleteMessages(
        0,
        new DirectMessagesResponse([Message("b-m", "b", "Thread B", "bob")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        0,
        new DirectMessageParticipantsResponse([Participant("b-p", "b", "me", "owner", "me")], new PageInfo(null, false, null)));
    await switchThread;

    Assert.Empty(viewModel.ParticipantUserResults);

    var secondSearch = viewModel.SearchParticipantUsersAsync("al", TestContext.Current.CancellationToken);
    service.CompleteSearch(0, new UsersSearchResponse([new UserSearchResult("u1", "alice")], new PageInfo(null, false, null)));
    Assert.Empty(viewModel.ParticipantUserResults);

    service.CompleteSearch(1, new UsersSearchResponse([new UserSearchResult("u2", "ally")], new PageInfo(null, false, null)));

    await Task.WhenAll(firstSearch, secondSearch, switchThread);

    Assert.Equal(["u2"], viewModel.ParticipantUserResults.Select(row => row.Id));
  }

  private static async Task LoadThreadAsync(
      DirectMessagesViewModel viewModel,
      QueuedDirectMessagesService service,
      int requestIndex,
      string conversationId,
      string title,
      string username)
  {
    var load = viewModel.SelectConversationAsync(conversationId, TestContext.Current.CancellationToken);
    service.CompleteConversation(requestIndex, new DirectConversationResponse(Conversation(conversationId, title, username)));
    service.CompleteMessages(
        requestIndex,
        new DirectMessagesResponse([Message($"{conversationId}-m", conversationId, title, username)], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        requestIndex,
        new DirectMessageParticipantsResponse([Participant($"{conversationId}-p", conversationId, "me", "owner", "me")], new PageInfo(null, false, null)));
    await load;
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

}
