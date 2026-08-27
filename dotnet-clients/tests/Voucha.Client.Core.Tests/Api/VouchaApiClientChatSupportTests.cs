using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiClientChatSupportTests
{
  [Fact]
  public async Task ChatAndSupportServiceMethodsUseExpectedRoutes()
  {
    var handler = new RecordingHandler([
      new RecordedResponse("""
          {
            "results": [
              {
                "id": "conversation-1",
                "title": "",
                "created_at": "2026-07-01T10:00:00Z",
                "created_by_id": "user-1",
                "updated_at": "2026-07-01T10:00:00Z",
                "updated_by_id": null,
                "deleted_at": null,
                "deleted_by_id": null
              }
            ],
            "page_info": { "has_next_page": false, "start_cursor": "conversation-1", "end_cursor": null }
          }
          """),
      new RecordedResponse("""
          {
            "conversation": {
              "id": "conversation-2",
              "title": "",
              "created_at": "2026-07-01T10:01:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:01:00Z",
              "updated_by_id": null,
              "deleted_at": null,
              "deleted_by_id": null
            }
          }
          """),
      new RecordedResponse("""
          {
            "results": [
              {
                "id": "message-1",
                "conversation_id": "conversation-1",
                "created_at": "2026-07-01T10:02:00Z",
                "created_by_id": "user-1",
                "updated_at": "2026-07-01T10:02:00Z",
                "updated_by_id": null,
                "deleted_at": null,
                "deleted_by_id": null,
                "content": { "role": "user", "content": "Hello", "error": null }
              }
            ],
            "page_info": { "has_next_page": false, "start_cursor": "message-1", "end_cursor": null }
          }
          """),
      new RecordedResponse("""
          {
            "conversation": {
              "id": "conversation-1",
              "title": "Updated",
              "created_at": "2026-07-01T10:00:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:03:00Z",
              "updated_by_id": "user-1",
              "deleted_at": null,
              "deleted_by_id": null
            }
          }
          """),
      new RecordedResponse("""
          {
            "conversation": {
              "id": "conversation-1",
              "title": "Generated",
              "created_at": "2026-07-01T10:00:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:04:00Z",
              "updated_by_id": "user-1",
              "deleted_at": null,
              "deleted_by_id": null
            }
          }
          """),
      new RecordedResponse("{}", HttpStatusCode.NoContent),
      new RecordedResponse("""
          {
            "user_message": {
              "id": "message-user",
              "conversation_id": "conversation-1",
              "created_at": "2026-07-01T10:04:01Z",
              "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:04:01Z",
              "updated_by_id": null,
              "deleted_at": null,
              "deleted_by_id": null,
              "content": { "role": "user", "content": "Hello", "error": null }
            },
            "assistant_message": {
              "id": "message-assistant",
              "conversation_id": "conversation-1",
              "created_at": "2026-07-01T10:04:02Z",
              "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:04:02Z",
              "updated_by_id": null,
              "deleted_at": null,
              "deleted_by_id": null,
              "content": { "role": "assistant", "content": "Hi", "error": null }
            },
            "agentic_run": {
              "id": "run-1",
              "conversation_id": "conversation-1",
              "conversation_message_id": "message-assistant",
              "parent_agentic_run_id": null,
              "model_name": "phi-silica",
              "model_provider": "windows_foundry",
              "input": { "message": "Hello" },
              "output": { "response": "Hi" },
              "error": null,
              "status": "completed",
              "termination_reason": "no_tool_calls",
              "started_at": "2026-07-01T10:04:02Z",
              "completed_at": "2026-07-01T10:04:02Z",
              "failed_at": null,
              "created_at": "2026-07-01T10:04:02Z",
              "updated_at": "2026-07-01T10:04:02Z",
              "deleted_at": null
            }
          }
          """),
      new RecordedResponse("""
          {
            "results": [
              {
                "id": "thread-1",
                "support_contact_id": "contact-1",
                "subject": "Need help",
                "conversation_id": "conversation-1",
                "created_at": "2026-07-01T10:05:00Z",
                "updated_at": "2026-07-01T10:05:00Z",
                "assigned_at": null,
                "assigned_to_id": null,
                "resolved_at": null,
                "resolved_by_id": null,
                "status": "open"
              }
            ],
            "page_info": { "has_next_page": false, "start_cursor": "thread-1", "end_cursor": null }
          }
          """),
      new RecordedResponse("""
          {
            "thread": {
              "id": "thread-1",
              "support_contact_id": "contact-1",
              "subject": "Need help",
              "conversation_id": "conversation-1",
              "created_at": "2026-07-01T10:05:00Z",
              "updated_at": "2026-07-01T10:05:00Z",
              "assigned_at": null,
              "assigned_to_id": null,
              "resolved_at": null,
              "resolved_by_id": null,
              "status": "open"
            },
            "messages": [],
            "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null }
          }
          """),
      new RecordedResponse("""
          {
            "thread": {
              "id": "thread-1",
              "support_contact_id": "contact-1",
              "subject": "Need help",
              "conversation_id": "conversation-1",
              "created_at": "2026-07-01T10:05:00Z",
              "updated_at": "2026-07-01T10:05:00Z",
              "assigned_at": null,
              "assigned_to_id": null,
              "resolved_at": null,
              "resolved_by_id": null,
              "status": "open"
            },
            "message": {
              "id": "support-message-1",
              "support_thread_id": "thread-1",
              "direction": "inbound",
              "body_text": "Need help",
              "body_html": "<p>Need help</p>",
              "created_at": "2026-07-01T10:05:00Z",
              "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:05:00Z",
              "email_message_id": null,
              "email_subject": null,
              "email_from": null,
              "email_to": null,
              "drafted_at": null,
              "edited_at": null,
              "edited_by_id": null,
              "approved_at": null,
              "approved_by_id": null,
              "sent_at": null
            }
          }
          """),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiChatService(client);

    var conversations = await service.FetchMyConversationsAsync("cursor-1", 13, TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/my/conversations?after=cursor-1&limit=13", handler.PathAndQuery);
    Assert.Equal("conversation-1", conversations.Results[0].Id);

    var created = await service.CreateConversationAsync(new CreateChatConversationBody(), TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/conversations", handler.PathAndQuery);
    Assert.Equal("conversation-2", created.Conversation.Id);

    var messages = await service.FetchConversationMessagesAsync("conversation-1", TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/my/conversations/conversation-1/messages", handler.PathAndQuery);
    Assert.Equal("message-1", messages.Results[0].Id);

    var updated = await service.UpdateConversationTitleAsync("conversation-1", "Updated", TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Patch, handler.Method);
    Assert.Equal("Updated", updated.Conversation.Title);

    var generated = await service.GenerateConversationTitleAsync("conversation-1", TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/my/conversations/conversation-1/title", handler.PathAndQuery);
    Assert.Equal("Generated", generated.Conversation.Title);

    await service.DeleteConversationAsync("conversation-1", TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Delete, handler.Method);

    var clientGenerated = await service.CreateClientGeneratedChatAsync(
        "conversation-1",
        new CreateClientGeneratedChatBody("Hello", "Hi", "windows_foundry"),
        TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/conversations/conversation-1/client-generated-chat", handler.PathAndQuery);
    Assert.Contains("\"model_provider\":\"windows_foundry\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.DoesNotContain("\"model_name\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Equal("phi-silica", clientGenerated.AgenticRun.ModelName);

    var supportThreads = await service.FetchSupportThreadsAsync("cursor-2", 9, TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/my/support-threads?after=cursor-2&limit=9", handler.PathAndQuery);
    Assert.Equal("thread-1", supportThreads.Results[0].Id);

    var supportThread = await service.FetchSupportThreadAsync(
        "thread-1",
        "cursor-3",
        9,
        TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/my/support-threads/thread-1?after=cursor-3&limit=9", handler.PathAndQuery);
    Assert.Equal("Need help", supportThread.Thread.Subject);

    var createdSupportThread = await service.CreateSupportThreadAsync(
        new CreateSupportThreadBody("Need help", "Need more info", "conversation-1"),
        TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/my/support-threads", handler.PathAndQuery);
    Assert.Equal("support-message-1", createdSupportThread.Message?.Id);
  }

  [Fact]
  public void ChatMessageContentDecodesLegacyStringContent()
  {
    var message = JsonSerializer.Deserialize<ChatMessage>(
        """
        {
          "id": "message-1",
          "conversation_id": "conversation-1",
          "created_at": "2026-07-01T10:02:00Z",
          "created_by_id": "user-1",
          "updated_at": "2026-07-01T10:02:00Z",
          "updated_by_id": null,
          "deleted_at": null,
          "deleted_by_id": null,
          "content": "plain text"
        }
        """,
        VouchaApiJson.Options);

    Assert.NotNull(message);
    Assert.Equal("message", message.Content.Role);
    Assert.Equal("plain text", message.Content.Content);
    Assert.Equal("plain text", message.Content.DisplayText);
    Assert.Null(message.Content.Error);
  }

  [Fact]
  public async Task StreamConversationAsyncParsesSseEvents()
  {
    var handler = new StreamingSseHandler(string.Join(
        "\r\n",
        [
          ": comment",
          "event: metadata",
          "data: {\"conversation_id\":\"conversation-1\",\"user_message_id\":\"user-1\",\"assistant_message_id\":\"assistant-1\",\"job_id\":\"job-1\"}",
          string.Empty,
          "data: {\"content\":\"Hello\"}",
          string.Empty,
          "event: tool_call",
          "data: {\"tool_call_id\":\"call-1\",\"name\":\"search\",\"arguments\":\"{}\"}",
          string.Empty,
          "event: subagent_step",
          "data: {\"agent_name\":\"planner\",\"tool_name\":\"search\",\"tool_call_id\":\"call-1\"}",
          string.Empty,
          "event: subagent_text",
          "data: {\"agent_name\":\"planner\",\"tool_call_id\":\"call-1\",\"content\":\"Working\"}",
          string.Empty,
          "event: tool_result",
          "data: {\"tool_call_id\":\"call-1\",\"result\":{\"status\":\"ok\"}}",
          string.Empty,
          "event: done",
          "data: {}",
        ]));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var events = new List<ChatStreamEvent>();
    await foreach (var streamEvent in client.StreamChatConversationAsync(
        "conversation-1",
        "Hello",
        TestContext.Current.CancellationToken))
    {
      events.Add(streamEvent);
    }

    Assert.Equal("/api/v1/conversations/conversation-1/chat", handler.PathAndQuery);
    Assert.IsType<ChatStreamMetadataEvent>(events[0]);
    Assert.IsType<ChatStreamTextEvent>(events[1]);
    Assert.IsType<ChatStreamToolCallEvent>(events[2]);
    Assert.IsType<ChatStreamSubagentStepEvent>(events[3]);
    Assert.IsType<ChatStreamSubagentTextEvent>(events[4]);
    var toolResult = Assert.IsType<ChatStreamToolResultEvent>(events[5]);
    Assert.Equal("call-1", toolResult.ToolCallId);
    Assert.Equal("ok", toolResult.Result.GetProperty("status").GetString());
    Assert.IsType<ChatStreamDoneEvent>(events[6]);
  }

  [Fact]
  public async Task StreamConversationAsyncParsesToolResultEvents()
  {
    var handler = new StreamingSseHandler(string.Join(
        "\n",
        [
          "event: tool_result",
          "data: {\"tool_call_id\":\"call-1\",\"result\":\"done\"}",
          string.Empty,
          "event: done",
          "data: {}",
        ]));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var events = new List<ChatStreamEvent>();
    await foreach (var streamEvent in client.StreamChatConversationAsync(
        "conversation-1",
        "Hello",
        TestContext.Current.CancellationToken))
    {
      events.Add(streamEvent);
    }

    Assert.Equal(2, events.Count);
    var toolResult = Assert.IsType<ChatStreamToolResultEvent>(events[0]);
    Assert.Equal("call-1", toolResult.ToolCallId);
    Assert.Equal(JsonValueKind.String, toolResult.Result.ValueKind);
    Assert.Equal("done", toolResult.Result.GetString());
    Assert.IsType<ChatStreamDoneEvent>(events[1]);
  }

  [Fact]
  public async Task StreamConversationAsyncRejectsUnexpectedContentType()
  {
    var handler = new StreamingSseHandler("event: done\r\ndata: {}\r\n", "application/json");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
      await foreach (var _ in client.StreamChatConversationAsync(
          "conversation-1",
          "Hello",
          TestContext.Current.CancellationToken))
      {
      }
    });

    Assert.Contains("unexpected SSE response content type", exception.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task StreamConversationAsyncParsesErrorsAndIgnoresMalformedEvents()
  {
    var handler = new StreamingSseHandler(string.Join(
        "\n",
        [
          "event: unknown",
          "data: {}",
          string.Empty,
          "event: text",
          "data: not-json",
          string.Empty,
          "event: error",
          "data: {\"error\":\"boom\"}",
          string.Empty,
        ]));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var events = new List<ChatStreamEvent>();
    await foreach (var streamEvent in client.StreamChatConversationAsync(
        "conversation-1",
        "Hello",
        TestContext.Current.CancellationToken))
    {
      events.Add(streamEvent);
    }

    var error = Assert.IsType<ChatStreamErrorEvent>(Assert.Single(events));
    Assert.Equal("boom", error.Error);
  }

  [Fact]
  public async Task StreamConversationAsyncThrowsApiExceptionForNonSuccess()
  {
    var handler = new StreamingSseHandler("{\"message\":\"nope\"}", statusCode: HttpStatusCode.BadRequest);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var exception = await Assert.ThrowsAsync<VouchaApiException>(async () =>
    {
      await foreach (var _ in client.StreamChatConversationAsync(
          "conversation-1",
          "Hello",
          TestContext.Current.CancellationToken))
      {
      }
    });

    Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    Assert.Contains("nope", exception.ResponseBody, StringComparison.Ordinal);
  }

  [Fact]
  public void NativeChatRoutePathsParseChatAndSupportRoutes()
  {
    Assert.True(NativeChatRoutePaths.IsChatRootPath("/chat"));
    Assert.True(NativeChatRoutePaths.IsChatSupportRootPath("/chat/support/new"));
    Assert.True(NativeChatRoutePaths.TryGetChatConversationId("/chat/conversation-1/messages", out var conversationId));
    Assert.Equal("conversation-1", conversationId);
    Assert.False(NativeChatRoutePaths.TryGetChatConversationId("/chat/support/thread-1", out _));

    Assert.True(NativeChatRoutePaths.TryGetSupportThreadId("/chat/support/thread-1", out var threadId));
    Assert.Equal("thread-1", threadId);
    Assert.False(NativeChatRoutePaths.TryGetSupportThreadId("/chat/support/new", out _));
  }

  private sealed class StreamingSseHandler : HttpMessageHandler
  {
    private readonly string body;
    private readonly string contentType;
    private readonly HttpStatusCode statusCode;

    public StreamingSseHandler(
        string body,
        string contentType = "text/event-stream",
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
      this.body = body;
      this.contentType = contentType;
      this.statusCode = statusCode;
    }

    public HttpMethod? Method { get; private set; }

    public string? PathAndQuery { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Method = request.Method;
      PathAndQuery = request.RequestUri?.PathAndQuery;
      var response = new HttpResponseMessage(statusCode)
      {
        Content = new StringContent(body, Encoding.UTF8, contentType),
        RequestMessage = request,
      };
      return Task.FromResult(response);
    }
  }
}
