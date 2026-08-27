using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class ApiDirectMessagesServiceTests
{
  [Fact]
  public async Task DelegatesEveryDirectMessageMethodToVouchaApiClient()
  {
    var handler = new RecordingHandler(new[]
    {
      new RecordedResponse("""
          {
            "results": [],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
          }
          """),
      new RecordedResponse("""
          {
            "conversation": {
              "id": "c1",
              "channel_type": "direct",
              "title": "Direct message",
              "created_at": "2026-07-01T10:00:00Z",
              "updated_at": "2026-07-01T10:00:00Z"
            }
          }
          """),
      new RecordedResponse("""
          {
            "conversation": {
              "id": "c1",
              "channel_type": "direct",
              "title": "Direct message",
              "created_at": "2026-07-01T10:00:00Z",
              "updated_at": "2026-07-01T10:00:00Z"
            }
          }
          """),
      new RecordedResponse("""
          {
            "results": [],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
          }
          """),
      new RecordedResponse("""
          {
            "message": {
              "id": "m1",
              "conversation_id": "c1",
              "body_text": "hello",
              "created_by_id": "u1",
              "created_at": "2026-07-01T10:00:00Z"
            }
          }
          """),
      new RecordedResponse("""
          {
            "results": [],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
          }
          """),
      new RecordedResponse("""
          {
            "participant": {
              "id": "p2",
              "conversation_id": "c1",
              "user_id": "u2",
              "role": "member",
              "created_at": "2026-07-01T10:00:00Z"
            }
          }
          """),
      new RecordedResponse("{}"),
      new RecordedResponse("""{"participant_add_policy":"all_members"}"""),
      new RecordedResponse("""
          {
            "results": [
              {
                "id": "u1",
                "username": "alice",
                "roles": []
              }
            ],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
          }
          """),
    });

    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiDirectMessagesService(client);

    await service.FetchDirectMessagesAsync(new FetchDirectMessagesRequest("cursor-1", 3), TestContext.Current.CancellationToken);
    await service.FetchDirectConversationAsync("c1", TestContext.Current.CancellationToken);
    await service.CreateDirectConversationAsync(new CreateDirectConversationRequest(["u1", "u2"]), TestContext.Current.CancellationToken);
    await service.FetchDirectConversationMessagesAsync(
        new FetchDirectConversationMessagesRequest("c1", "cursor-2", 4),
        TestContext.Current.CancellationToken);
    await service.SendDirectMessageAsync(new SendDirectMessageRequest("c1", "hello"), TestContext.Current.CancellationToken);
    await service.FetchDirectConversationParticipantsAsync("c1", TestContext.Current.CancellationToken);
    await service.AddDirectConversationParticipantAsync(
        new AddDirectConversationParticipantRequest("c1", "u2"),
        TestContext.Current.CancellationToken);
    await service.RemoveDirectConversationParticipantAsync("c1", "u2", TestContext.Current.CancellationToken);
    await service.UpdateDirectConversationParticipantPolicyAsync(
        new UpdateDirectConversationParticipantPolicyRequest("c1", "all_members"),
        TestContext.Current.CancellationToken);
    var search = await service.SearchUsersAsync(new SearchUsersRequest("al", Limit: 2), TestContext.Current.CancellationToken);

    Assert.Equal(10, handler.Requests.Count);
    Assert.Equal((HttpMethod.Get, "/api/v1/my/messages?after=cursor-1&limit=3"), RequestAt(handler, 0));
    Assert.Equal((HttpMethod.Get, "/api/v1/my/messages/c1"), RequestAt(handler, 1));
    Assert.Equal((HttpMethod.Post, "/api/v1/my/messages"), RequestAt(handler, 2));
    Assert.Equal((HttpMethod.Get, "/api/v1/my/messages/c1/messages?after=cursor-2&limit=4"), RequestAt(handler, 3));
    Assert.Equal((HttpMethod.Post, "/api/v1/my/messages/c1/messages"), RequestAt(handler, 4));
    Assert.Equal((HttpMethod.Get, "/api/v1/my/messages/c1/participants"), RequestAt(handler, 5));
    Assert.Equal((HttpMethod.Post, "/api/v1/my/messages/c1/participants"), RequestAt(handler, 6));
    Assert.Equal((HttpMethod.Delete, "/api/v1/my/messages/c1/participants/u2"), RequestAt(handler, 7));
    Assert.Equal((HttpMethod.Patch, "/api/v1/my/messages/c1"), RequestAt(handler, 8));
    Assert.Equal((HttpMethod.Get, "/api/v1/users?limit=2&q=al"), RequestAt(handler, 9));

    Assert.Equal("{\"user_ids\":[\"u1\",\"u2\"]}", handler.Requests[2].Body);
    Assert.Equal("{\"text\":\"hello\"}", handler.Requests[4].Body);
    Assert.Equal("{\"user_id\":\"u2\"}", handler.Requests[6].Body);
    Assert.Equal("{\"participant_add_policy\":\"all_members\"}", handler.Requests[8].Body);
    Assert.Equal("alice", search.Results[0].Username);
  }

  private static (HttpMethod? Method, string? PathAndQuery) RequestAt(RecordingHandler handler, int index) =>
      (handler.Requests[index].Method, handler.Requests[index].PathAndQuery);
}
