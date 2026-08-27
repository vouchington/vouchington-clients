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
