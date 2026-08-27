using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchDirectMessagesAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("native.messages.conversations.default");

    var response = await client.FetchDirectMessagesAsync(
        new FetchDirectMessagesRequest(Limit: 17),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/messages?limit=17");
    Assert.Equal("00000000-0000-7000-8000-000000000101", response.Results[0].Id);
    Assert.Equal("all_members", response.Results[0].ParticipantAddPolicy);
    Assert.Equal("alice", response.Results[0].ParticipantUsernames?.First());
  }

  [Fact]
  public async Task FetchDirectConversationAsyncUsesConversationEndpoint()
  {
    var (client, handler) = CreateClient("native.messages.conversation.default");

    var response = await client.FetchDirectConversationAsync(
        "00000000-0000-7000-8000-000000000101",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/messages/00000000-0000-7000-8000-000000000101");
    Assert.Equal("Alice, Bob", response.Conversation.Title);
  }

  [Fact]
  public async Task CreateDirectConversationAsyncPostsUserIds()
  {
    var (client, handler) = CreateClient("native.messages.create.default");

    await client.CreateDirectConversationAsync(
        new CreateDirectConversationRequest(["user-1", "user-2"]),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/my/messages");
    AssertJsonBody(handler, "user_ids", JsonValueKind.Array);
  }

  [Fact]
  public async Task FetchDirectConversationMessagesAsyncForwardsCursorAndLimit()
  {
    var (client, handler) = CreateClient("native.messages.thread.default");

    var response = await client.FetchDirectConversationMessagesAsync(
        new FetchDirectConversationMessagesRequest(
            "00000000-0000-7000-8000-000000000101",
            "00000000-0000-7000-8000-000000000201",
            9),
        TestContext.Current.CancellationToken);

    AssertRequest(
        handler,
        HttpMethod.Get,
        "/api/v1/my/messages/00000000-0000-7000-8000-000000000101/messages?after=00000000-0000-7000-8000-000000000201&limit=9");
    Assert.Equal("Hello from Alice", response.Results[0].BodyText);
  }

  [Fact]
  public async Task SendDirectMessageAsyncPostsText()
  {
    var (client, handler) = CreateClient("native.messages.send.default");

    var response = await client.SendDirectMessageAsync(
        new SendDirectMessageRequest("00000000-0000-7000-8000-000000000101", "Sent from native"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/my/messages/00000000-0000-7000-8000-000000000101/messages");
    AssertJsonBody(handler, "text", JsonValueKind.String);
    Assert.Equal("Sent from native", response.Message.BodyText);
  }

  [Fact]
  public async Task ParticipantEndpointsUseSharedFixtures()
  {
    var (client, handler) = CreateClient(
        "native.messages.participants.default",
        "native.messages.participant-add.default",
        "native.messages.policy.default");

    var participants = await client.FetchDirectConversationParticipantsAsync(
        "00000000-0000-7000-8000-000000000101",
        TestContext.Current.CancellationToken);
    var added = await client.AddDirectConversationParticipantAsync(
        new AddDirectConversationParticipantRequest(
            "00000000-0000-7000-8000-000000000101",
            "00000000-0000-7000-8000-000000000003"),
        TestContext.Current.CancellationToken);
    var policy = await client.UpdateDirectConversationParticipantPolicyAsync(
        new UpdateDirectConversationParticipantPolicyRequest(
            "00000000-0000-7000-8000-000000000101",
            "owner_only"),
        TestContext.Current.CancellationToken);

    Assert.Equal("fixtureuser", participants.Results[0].Username);
    Assert.Equal("bob", added.Participant.Username);
    Assert.Equal("owner_only", policy.ParticipantAddPolicy);
    Assert.Equal(HttpMethod.Patch, handler.Requests.Last().Method);
  }

  [Fact]
  public async Task RemoveDirectConversationParticipantAsyncUsesDeleteEndpoint()
  {
    var (client, handler) = CreateClient("native.messages.participants.default");

    await client.RemoveDirectConversationParticipantAsync(
        "00000000-0000-7000-8000-000000000101",
        "00000000-0000-7000-8000-000000000003",
        TestContext.Current.CancellationToken);

    AssertRequest(
        handler,
        HttpMethod.Delete,
        "/api/v1/my/messages/00000000-0000-7000-8000-000000000101/participants/00000000-0000-7000-8000-000000000003");
  }

  [Fact]
  public async Task SearchUsersAsyncUsesAuthenticatedUserSearchEndpoint()
  {
    var (client, handler) = CreateClient("native.messages.user-search.default");

    var response = await client.SearchUsersAsync(
        new SearchUsersRequest("bo", Limit: 10),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/users?limit=10&q=bo");
    Assert.Equal("bob", response.Results[0].Username);
  }

  [Fact]
  public async Task SearchUsersAsyncForwardsTheContinuationCursor()
  {
    var (client, handler) = CreateClient("native.messages.user-search.default");

    await client.SearchUsersAsync(
        new SearchUsersRequest("bo", "cursor-1", 10),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/users?after=cursor-1&limit=10&q=bo");
  }

  private static void AssertJsonBody(RecordingHandler handler, string property, JsonValueKind kind)
  {
    using var document = JsonDocument.Parse(handler.RequestBody ?? "{}");
    Assert.Equal(kind, document.RootElement.GetProperty(property).ValueKind);
  }
}
