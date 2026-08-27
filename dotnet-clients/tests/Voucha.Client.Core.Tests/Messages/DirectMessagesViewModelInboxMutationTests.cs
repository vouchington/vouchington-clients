using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class DirectMessagesViewModelInboxMutationTests
{
  [Fact]
  public async Task CreateConversationUpsertsExistingInboxRowWhenReloadFails()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "Old title")],
          new PageInfo(null, false, null)),
      ConversationResponse = new DirectConversationResponse(
          new DirectConversation("c1", "direct", "", Time(), Time(), null, ["Alice"], "owner_only")),
      SendResponse = new DirectMessageResponse(
          new DirectMessage("server-1", "c1", "sent", "me", Time(10, 5, 0), "me")),
      MessagesResponse = new DirectMessagesResponse([], new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse([], new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");
    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    service.FetchDirectMessagesException = new InvalidOperationException("reload failed");

    Assert.True(await viewModel.CreateConversationAsync(
        ["u1"],
        " hi ",
        cancellationToken: TestContext.Current.CancellationToken));

    var row = Assert.Single(viewModel.Conversations);
    Assert.Equal("Alice", row.Title);
    Assert.Equal(Time(10, 5, 0), row.UpdatedAt);
    Assert.Equal([null, null], service.FetchRequests.Select(request => request.After));
  }

  [Fact]
  public async Task AddParticipantRefreshesInboxConversationRow()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "")],
          new PageInfo(null, false, null)),
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "")),
      MessagesResponse = new DirectMessagesResponse([], new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "alice")],
          new PageInfo(null, false, null)),
      ParticipantResponse = new DirectMessageParticipantResponse(Participant("p2", "c1", "u2", "member", "bob")),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");
    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.ConversationResponse = new DirectConversationResponse(new DirectConversation(
        "c1",
        "direct",
        "",
        Time(),
        Time(10, 6, 0),
        null,
        [],
        "all_members"));
    service.ParticipantsResponse = new DirectMessageParticipantsResponse(
        [Participant("p1", "c1", "me", "owner", "alice"), Participant("p2", "c1", "u2", "member", "bob")],
        new PageInfo(null, false, null));

    await viewModel.AddParticipantAsync("u2", TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Conversations);
    Assert.Equal("bob", row.Title);
    Assert.Equal(Time(10, 6, 0), row.UpdatedAt);
    Assert.Equal("all_members", viewModel.ParticipantAddPolicy);
    Assert.Equal(["me", "u2"], viewModel.Participants.Select(participant => participant.UserId));
    Assert.Equal(["c1", "c1"], service.ConversationRequests);
    Assert.Equal(["c1", "c1"], service.ParticipantRequests);
  }

  [Fact]
  public async Task RemoveParticipantRefreshesInboxConversationRow()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "")],
          new PageInfo(null, false, null)),
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "")),
      MessagesResponse = new DirectMessagesResponse([], new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "alice"), Participant("p2", "c1", "u2", "member", "bob")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");
    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.ConversationResponse = new DirectConversationResponse(new DirectConversation(
        "c1",
        "direct",
        "",
        Time(),
        Time(10, 7, 0),
        null,
        [],
        "owner_only"));
    service.ParticipantsResponse = new DirectMessageParticipantsResponse(
        [Participant("p1", "c1", "me", "owner", "alice")],
        new PageInfo(null, false, null));

    await viewModel.RemoveParticipantAsync("u2", TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Conversations);
    Assert.Equal("", row.Title);
    Assert.Equal(Time(10, 7, 0), row.UpdatedAt);
    Assert.Equal("owner_only", viewModel.ParticipantAddPolicy);
    Assert.Equal(["me"], viewModel.Participants.Select(participant => participant.UserId));
    Assert.Equal(["c1", "c1"], service.ConversationRequests);
    Assert.Equal(["c1", "c1"], service.ParticipantRequests);
  }

  [Fact]
  public async Task SendMessageBumpsInboxConversationRowWhenInboxReloadFails()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "Old title")],
          new PageInfo(null, false, null)),
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Old title", "Alice")),
      MessagesResponse = new DirectMessagesResponse(
          [Message("m1", "c1", "before", "me")],
          new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me")],
          new PageInfo(null, false, null)),
      SendResponse = new DirectMessageResponse(Message("server-1", "c1", "sent", "me", Time(10, 5, 0), "me")),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");
    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.FetchDirectMessagesException = new InvalidOperationException("reload failed");

    Assert.True(await viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken));

    var row = Assert.Single(viewModel.Conversations);
    Assert.Equal("Old title", row.Title);
    Assert.Equal(Time(10, 5, 0), row.UpdatedAt);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("reload failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SendMessageKeepsLocalSenderLabelWhenThreadReloadFails()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "Old title", "Alice")],
          new PageInfo(null, false, null)),
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Old title", "Alice")),
      MessagesResponse = new DirectMessagesResponse(
          [Message("m1", "c1", "before", "me")],
          new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me")],
          new PageInfo(null, false, null)),
      SendResponse = new DirectMessageResponse(Message("server-1", "c1", "sent", "me", Time(10, 5, 0))),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");
    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.FetchDirectConversationMessagesException = new InvalidOperationException("thread reload failed");

    Assert.True(await viewModel.SendMessageAsync(" sent ", TestContext.Current.CancellationToken));

    Assert.Equal("You", Assert.Single(viewModel.Messages, message => message.Id == "server-1").SenderLabel);
    Assert.Equal(LoadState.Error, viewModel.ThreadState);
    Assert.Equal("thread reload failed", viewModel.ThreadErrorMessage);
  }

  private static DirectConversation Conversation(string id, string title, params string[] names) =>
      new(id, "direct", title, Time(), Time(), null, names, "owner_only");

  private static DirectMessage Message(
      string id,
      string conversationId,
      string text,
      string? username,
      DateTimeOffset? createdAt = null,
      string? senderUsername = null) =>
      new(id, conversationId, text, username, createdAt ?? Time(), senderUsername);

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
