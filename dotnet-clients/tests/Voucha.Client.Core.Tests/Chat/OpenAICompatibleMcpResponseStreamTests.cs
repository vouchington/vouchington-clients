using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class OpenAICompatibleMcpResponseStreamTests
{
  [Fact]
  public async Task BrokenResponseStreamBeforeToolAdmissionBecomesHandledSendError()
  {
    using var handler = new BrokenResponseHandler();
    using var client = new OpenAICompatibleResponsesClient(handler);
    using var schema = JsonDocument.Parse("""{"type":"object"}""");
    var profile = new LocalLLMEndpointProfile(
        Guid.NewGuid(), "Test", true, "http://127.0.0.1:11434/v1", ["model"], "model");
    var calls = 0;

    var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
        client.GenerateAssistantContentWithToolsAsync(
            "Read", [], profile, null, [new("read_profile", "", schema.RootElement.Clone())],
            () => true, null,
            (_, _, _) => { calls++; return Task.FromResult("{}"); },
            TestContext.Current.CancellationToken));

    Assert.IsType<IOException>(error.InnerException);
    Assert.Equal(0, calls);
  }

  private sealed class BrokenResponseHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StreamContent(new BrokenReadStream()),
        });
  }

  private sealed class BrokenReadStream : Stream
  {
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Connection reset.");
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<int>(new IOException("Connection reset."));
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
  }
}
