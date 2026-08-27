using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;
using Xunit;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class DirectMessagesViewModelCreateRaceTests
{
  [Fact]
  public async Task CreateConversationDoesNotOverwriteSwitchedThreadWhenInitialSendCompletes()
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

    var create = viewModel.CreateConversationAsync(
        ["u1"],
        " hello ",
        ["Alice"],
        TestContext.Current.CancellationToken);
    service.CompleteCreateConversation(new DirectConversationResponse(Conversation("c-new", "", "Alice")));

    for (var attempt = 0; attempt < 20 && service.SendRequests.Count == 0; attempt++)
    {
      await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    Assert.Single(service.SendRequests);

    var secondLoad = viewModel.SelectConversationAsync("c2", TestContext.Current.CancellationToken);
    service.CompleteConversation("c2", new DirectConversationResponse(Conversation("c2", "Thread two", "Bob")));
    service.CompleteMessages(
        "c2",
        new DirectMessagesResponse([Message("m2", "c2", "new", "bob")], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c2",
        new DirectMessageParticipantsResponse([Participant("p2", "c2", "me", "owner", "me")], new PageInfo(null, false, null)));

    service.CompleteSend("c-new", new DirectMessageResponse(Message("server-1", "c-new", "hello", "me")));

    for (var attempt = 0; attempt < 20 && service.ConversationFetchRequests.Count == 0; attempt++)
    {
      await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    Assert.Single(service.ConversationFetchRequests);
    service.CompleteConversations(
        new DirectConversationsResponse([Conversation("c2", "Thread two", "Bob")], new PageInfo(null, false, null)));

    await Task.WhenAll(create, secondLoad);

    Assert.Equal("c2", viewModel.SelectedConversationId);
    Assert.Equal(["m2"], viewModel.Messages.Select(row => row.Id));
    Assert.DoesNotContain(viewModel.Messages, row => row.Id == "server-1");
    Assert.Equal(["c-new", "c2"], viewModel.Conversations.Select(row => row.Id));
  }

  private static DirectConversation Conversation(string id, string title, params string[] names) =>
      new(id, "direct", title, Time(), Time(), null, names, "owner_only");

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
}
