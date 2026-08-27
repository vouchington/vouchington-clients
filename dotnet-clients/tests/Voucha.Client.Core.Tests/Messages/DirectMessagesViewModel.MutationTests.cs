using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class DirectMessagesViewModelMutationTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ParticipantMutationsIgnoreStaleConversationChanges(bool fail)
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstLoad = viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    service.CompleteConversation("c1", new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")));
    service.CompleteMessages("c1", new DirectMessagesResponse([], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c1",
        new DirectMessageParticipantsResponse([Participant("p1", "c1", "me", "owner", "me")], new PageInfo(null, false, null)));
    await firstLoad;

    var add = viewModel.AddParticipantAsync("u2", TestContext.Current.CancellationToken);
    var remove = viewModel.RemoveParticipantAsync("u2", TestContext.Current.CancellationToken);
    var policy = viewModel.UpdateParticipantAddPolicyAsync("all_members", TestContext.Current.CancellationToken);
    var secondLoad = viewModel.SelectConversationAsync("c2", TestContext.Current.CancellationToken);

    if (fail)
    {
      service.FailAddParticipant("c1", new InvalidOperationException("add failed"));
      service.FailRemoveParticipant("c1", new InvalidOperationException("remove failed"));
      service.FailPolicyUpdate("c1", new InvalidOperationException("policy failed"));
    }
    else
    {
      service.CompleteAddParticipant("c1", new DirectMessageParticipantResponse(Participant("p2", "c1", "u2", "member", "alice")));
      service.CompleteRemoveParticipant("c1");
      service.CompletePolicyUpdate("c1", new DirectConversationPolicyResponse("all_members"));
    }

    await Task.WhenAll(add, remove, policy);

    Assert.Equal("c2", viewModel.SelectedConversationId);
    Assert.Empty(viewModel.Participants);
    Assert.Equal("owner_only", viewModel.ParticipantAddPolicy);
    Assert.Null(viewModel.ThreadErrorMessage);
    Assert.Equal(LoadState.Loading, viewModel.ThreadState);
    Assert.Equal("c1", service.AddParticipantRequests[0].ConversationId);
    Assert.Equal("c1", service.RemoveParticipantRequests[0].ConversationId);
    Assert.Equal("c1", service.PolicyRequests[0].ConversationId);

    service.CompleteConversation("c2", new DirectConversationResponse(Conversation("c2", "Thread two", "Bob")));
    service.CompleteMessages("c2", new DirectMessagesResponse([], new PageInfo(null, false, null)));
    service.CompleteParticipants(
        "c2",
        new DirectMessageParticipantsResponse([Participant("p3", "c2", "me", "owner", "me")], new PageInfo(null, false, null)));
    await secondLoad;
  }

  [Fact]
  public async Task CreateConversationIgnoresConcurrentDoubleClickRequests()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var create = viewModel.CreateConversationAsync(["u1"], " hello ", cancellationToken: TestContext.Current.CancellationToken);
    var duplicateCreate = viewModel.CreateConversationAsync(["u1"], " hello ", cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(duplicateCreate.IsCompletedSuccessfully);
    Assert.False(await duplicateCreate);
    Assert.Single(service.CreateRequests);

    service.FailCreateConversation(new InvalidOperationException("create failed"));
    await create;

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Single(service.CreateRequests);
    Assert.Empty(service.SendRequests);
  }

  [Fact]
  public async Task CreateConversationRetainsCreatedThreadWhenInitialSendFails()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")),
      SendDirectMessageException = new InvalidOperationException("send failed"),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    var created = await viewModel.CreateConversationAsync(
        ["u1", "u2"],
        " hello ",
        ["Alice", "Bob"],
        TestContext.Current.CancellationToken);

    Assert.False(created);
    Assert.Equal("c1", viewModel.SelectedConversationId);
    var row = Assert.Single(viewModel.Conversations);
    Assert.Equal("Alice", row.Title);
    Assert.Empty(viewModel.Messages);
    Assert.Equal("send failed", viewModel.ErrorMessage);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Single(service.CreateRequests);
    Assert.Single(service.SendRequests);
  }

  [Fact]
  public async Task ClearSelectedConversationStateResetsThreadFields()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")),
      MessagesResponse = new DirectMessagesResponse(
          [new DirectMessage("m1", "c1", "before", "me", Time(), "me")],
          new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);
    viewModel.ClearSelectedConversationState();

    Assert.Null(viewModel.SelectedConversationId);
    Assert.Empty(viewModel.Messages);
    Assert.Empty(viewModel.Participants);
    Assert.Equal(LoadState.Idle, viewModel.ThreadState);
    Assert.Null(viewModel.ThreadErrorMessage);
    Assert.True(viewModel.HasMoreMessages);
    Assert.Equal("owner_only", viewModel.ParticipantAddPolicy);
  }

  [Fact]
  public async Task RemoveParticipantClearsThreadAndInboxWhenLeavingSelf()
  {
    var service = new RecordingDirectMessagesService
    {
      ConversationsResponse = new DirectConversationsResponse(
          [Conversation("c1", "Thread one", "Alice"), Conversation("c2", "Thread two", "Bob")],
          new PageInfo(null, false, null)),
      ConversationResponse = new DirectConversationResponse(Conversation("c1", "Thread one", "Alice")),
      MessagesResponse = new DirectMessagesResponse([], new PageInfo(null, false, null)),
      ParticipantsResponse = new DirectMessageParticipantsResponse(
          [Participant("p1", "c1", "me", "owner", "me"), Participant("p2", "c1", "u2", "member", "alice")],
          new PageInfo(null, false, null)),
    };
    var viewModel = new DirectMessagesViewModel(service, "me");

    await viewModel.LoadInboxAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectConversationAsync("c1", TestContext.Current.CancellationToken);

    await viewModel.RemoveParticipantAsync("me", TestContext.Current.CancellationToken);

    Assert.Null(viewModel.SelectedConversationId);
    Assert.Empty(viewModel.Messages);
    Assert.Empty(viewModel.Participants);
    Assert.Equal(LoadState.Idle, viewModel.ThreadState);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal(["c2"], viewModel.Conversations.Select(row => row.Id));
    Assert.Equal(("c1", "me"), service.RemoveRequests[0]);
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

  private static DateTimeOffset Time() => new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
}
