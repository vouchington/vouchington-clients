using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ChatTranscriptTransportTests
{
  [Theory]
  [InlineData("native.chat.completed")]
  [InlineData("native.chat.duplicate")]
  [InlineData("native.chat.retry")]
  public async Task ServicePreservesServerTurnForCompletedDuplicateAndRetry(string fixtureId)
  {
    var fixture = Fixture(fixtureId);
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse(fixtureId), (HttpStatusCode)fixture.Status);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiChatService(new VouchaApiClient(http));
    var body = fixture.RequestBody!.Value.Deserialize<CreateClientGeneratedChatBody>(VouchaApiJson.Options)!;

    var result = await service.CreateClientGeneratedChatAsync(
        ConversationId(fixture), body, TestContext.Current.CancellationToken);

    Assert.Equal(body.UserMessageId, result.Turn.UserMessageId);
    Assert.Equal(body.AssistantMessageId, result.Turn.AssistantMessageId);
    Assert.Equal(result.Turn.UserMessageId, result.UserMessage.Id);
    Assert.Equal(result.Turn.AssistantMessageId, result.AssistantMessage.Id);
    Assert.Equal("completed", result.AssistantMessage.Completion!.Status);
    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal(fixture.Path, handler.PathAndQuery);
    Assert.Equal(body, JsonSerializer.Deserialize<CreateClientGeneratedChatBody>(handler.RequestBody!, VouchaApiJson.Options));
  }

  [Theory]
  [InlineData("native.chat.unauthorized", HttpStatusCode.Unauthorized)]
  [InlineData("native.chat.forbidden", HttpStatusCode.Forbidden)]
  [InlineData("native.chat.identity-conflict", HttpStatusCode.Conflict)]
  public async Task ServiceSurfacesAuthorizationOwnershipAndIdentityErrorsWithoutRetry(
      string fixtureId, HttpStatusCode expectedStatus)
  {
    var fixture = Fixture(fixtureId);
    Assert.Equal((int)expectedStatus, fixture.Status);
    var response = ApiFixtureLoader.LoadResponse(fixtureId);
    var handler = new RecordingHandler(response, expectedStatus);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiChatService(new VouchaApiClient(http));
    var body = fixture.RequestBody!.Value.Deserialize<CreateClientGeneratedChatBody>(VouchaApiJson.Options)!;

    var error = await Assert.ThrowsAsync<VouchaApiException>(() => service.CreateClientGeneratedChatAsync(
        ConversationId(fixture), body, TestContext.Current.CancellationToken));

    Assert.Equal(expectedStatus, error.StatusCode);
    Assert.Equal(response, error.ResponseBody);
    Assert.Equal(fixture.Path, handler.PathAndQuery);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task MessagePaginationForwardsTheOpaqueServerCursorThroughTheService()
  {
    var handler = new RecordingHandler(new[] { "native.chat.page-1", "native.chat.page-2" }
        .Select(id => new RecordedResponse(ApiFixtureLoader.LoadResponse(id))));
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiChatService(new VouchaApiClient(http));
    var conversation = ConversationId(Fixture("native.chat.completed"));

    var first = await service.FetchConversationMessagesPageAsync(conversation, null, 1, TestContext.Current.CancellationToken);
    Assert.True(first.PageInfo.HasNextPage);
    Assert.False(string.IsNullOrEmpty(first.PageInfo.EndCursor));
    var next = await service.FetchConversationMessagesPageAsync(
        conversation, first.PageInfo.EndCursor, 1, TestContext.Current.CancellationToken);

    Assert.False(next.PageInfo.HasNextPage);
    Assert.Single(first.Results);
    Assert.Single(next.Results);
    Assert.NotEqual(first.Results[0].Id, next.Results[0].Id);
    Assert.Equal($"/api/v1/my/conversations/{conversation}/messages?after={Uri.EscapeDataString(first.PageInfo.EndCursor!)}&limit=1",
        handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task LostPersistenceResponseRetriesTheSameSerializedTurnWithoutRegenerating()
  {
    using var handler = new LostResponseHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiChatService(new VouchaApiClient(http));
    var provider = new LocalProvider();
    using var model = new ChatConversationViewModel(service, provider, provider);
    await model.LoadAsync(ConversationId(Fixture("native.chat.completed")), "Existing", TestContext.Current.CancellationToken);

    Assert.False(await model.TrySendAsync("Hello", TestContext.Current.CancellationToken));
    Assert.Single(handler.PostBodies);
    Assert.Equal(1, provider.Generations);
    Assert.True(await model.TrySendAsync("Hello", TestContext.Current.CancellationToken));

    Assert.Equal(1, provider.Generations);
    Assert.Equal(2, handler.PostBodies.Count);
    Assert.Equal(handler.PostBodies[0], handler.PostBodies[1]);
    var body = JsonSerializer.Deserialize<CreateClientGeneratedChatBody>(handler.PostBodies[1], VouchaApiJson.Options)!;
    Assert.NotEqual(body.UserMessageId, body.AssistantMessageId);
    Assert.Equal("Hello", body.Message);
    Assert.Equal("Local reply", body.AssistantContent);
    var saved = JsonSerializer.Deserialize<ClientGeneratedChatResponse>(handler.SavedResponse!, VouchaApiJson.Options)!;
    Assert.Equal(body.UserMessageId, saved.Turn.UserMessageId);
    Assert.Equal(body.AssistantMessageId, saved.AssistantMessage.Id);
    Assert.Equal(body.AssistantContent, saved.AssistantMessage.Content.Content);
    Assert.Equal(ConversationId(Fixture("native.chat.completed")), model.ConversationId);
    Assert.False(model.IsStreaming);
  }

  private static ApiFixtureLoader.ApiFixtureManifestEntry Fixture(string id) =>
      ApiFixtureLoader.ManifestFixtures.Single(fixture => fixture.Id == id);

  private static string ConversationId(ApiFixtureLoader.ApiFixtureManifestEntry fixture) =>
      fixture.Path.Split('/')[4];

  private sealed class LocalProvider : ILocalChatProvider, IChatProviderResolver
  {
    public int Generations { get; private set; }
    public string Id => "local-test";
    public ChatProviderStatus Status { get; } = new(ChatProviderKind.Local, "Local", true,
        "Ready", "openai_compatible", "local-test");
    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [Status];
    public ChatProviderStatus GetDefaultProviderStatus() => Status;
    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(string message,
        IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default)
    {
      Generations++;
      return Task.FromResult(new LocalChatGenerationResult("Local reply", "openai_compatible", "local-test"));
    }
  }

  private sealed class LostResponseHandler : HttpMessageHandler
  {
    public List<string> PostBodies { get; } = [];
    public string? SavedResponse { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
      string fixtureId;
      if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/client-generated-chat", StringComparison.Ordinal))
      {
        PostBodies.Add(await request.Content!.ReadAsStringAsync(token));
        if (PostBodies.Count == 1) throw new HttpRequestException("Persistence response lost.");
        fixtureId = "native.chat.retry";
        // A synthetic lost-response scenario binds the real fixture shape to this generated turn.
        var body = JsonSerializer.Deserialize<CreateClientGeneratedChatBody>(PostBodies[^1], VouchaApiJson.Options)!;
        var response = JsonNode.Parse(ApiFixtureLoader.LoadResponse(fixtureId))!;
        response["turn"]!["user_message_id"] = body.UserMessageId;
        response["turn"]!["assistant_message_id"] = body.AssistantMessageId;
        response["user_message"]!["id"] = body.UserMessageId;
        response["assistant_message"]!["id"] = body.AssistantMessageId;
        response["user_message"]!["content"]!["content"] = body.Message;
        response["assistant_message"]!["content"]!["content"] = body.AssistantContent;
        SavedResponse = response.ToJsonString();
      }
      else if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal))
      {
        fixtureId = "native.chat.page-1";
      }
      else throw new InvalidOperationException($"Unexpected transcript request: {request.Method} {request.RequestUri}");
      return new HttpResponseMessage((HttpStatusCode)Fixture(fixtureId).Status)
      {
        Content = new StringContent(request.Method == HttpMethod.Post ? SavedResponse! : ApiFixtureLoader.LoadResponse(fixtureId),
            Encoding.UTF8, "application/json"),
      };
    }
  }
}
