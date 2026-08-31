using System.Text;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ChatStreamTerminationTests
{
  [Fact]
  public async Task ThrowsIncompleteStreamWhenEofArrivesBeforeTerminalEvent()
  {
    var client = CreateClient("event: text\ndata: {\"content\":\"partial\"}\n\n");

    await Assert.ThrowsAsync<ChatStreamIncompleteException>(async () =>
    {
      await foreach (var _ in client.StreamChatConversationAsync(
          "conversation-1", "Hello", TestContext.Current.CancellationToken)) { }
    });
  }

  [Fact]
  public async Task TreatsNamedErrorAsTerminal()
  {
    var client = CreateClient("event: error\ndata: {\"error\":\"failed\"}\n\n");
    var events = new List<ChatStreamEvent>();

    await foreach (var streamEvent in client.StreamChatConversationAsync(
        "conversation-1", "Hello", TestContext.Current.CancellationToken))
    {
      events.Add(streamEvent);
    }

    var error = Assert.IsType<ChatStreamErrorEvent>(Assert.Single(events));
    Assert.Equal("failed", error.Error);
  }

  [Fact]
  public async Task RejectsAnUnterminatedOversizedLine()
  {
    var client = CreateClient(new string('x', 1_024 * 1_024 + 1));

    await Assert.ThrowsAsync<ChatStreamFrameTooLargeException>(async () =>
    {
      await foreach (var _ in client.StreamChatConversationAsync(
          "conversation-1", "Hello", TestContext.Current.CancellationToken)) { }
    });
  }

  [Fact]
  public async Task AcceptsManyCompleteFramesInOneTransportRead()
  {
    const int frameCount = 100_000;
    var client = CreateClient(string.Concat(Enumerable.Repeat("event: text\ndata: {\"content\":\"x\"}\n\n", frameCount)) + "event: done\n\n");
    var received = 0;

    await foreach (var _ in client.StreamChatConversationAsync(
        "conversation-1", "Hello", TestContext.Current.CancellationToken))
    {
      received++;
    }

    Assert.Equal(frameCount + 1, received);
  }

  [Theory]
  [InlineData("\r")]
  [InlineData("\r\n")]
  public async Task AcceptsCarriageReturnSseLineEndings(string lineEnding)
  {
    var client = CreateClient(
        $"event: text{lineEnding}data: {{\"content\":\"hello\"}}{lineEnding}{lineEnding}" +
        $"event: done{lineEnding}{lineEnding}");
    var events = new List<ChatStreamEvent>();

    await foreach (var streamEvent in client.StreamChatConversationAsync(
        "conversation-1", "Hello", TestContext.Current.CancellationToken))
    {
      events.Add(streamEvent);
    }

    Assert.Collection(
        events,
        streamEvent => Assert.Equal("hello", Assert.IsType<ChatStreamTextEvent>(streamEvent).Content),
        streamEvent => Assert.IsType<ChatStreamDoneEvent>(streamEvent));
  }

  private static VouchaApiClient CreateClient(string body) =>
      new(new HttpClient(new StreamingHandler(body)) { BaseAddress = new Uri("https://api.test") });

  private sealed class StreamingHandler(string body) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage
        {
          Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
          RequestMessage = request,
        });
  }
}
