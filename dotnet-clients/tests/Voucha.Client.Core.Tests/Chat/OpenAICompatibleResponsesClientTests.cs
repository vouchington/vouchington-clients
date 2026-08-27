using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class OpenAICompatibleResponsesClientTests
{
  [Fact]
  public async Task GenerateAssistantContentAsyncPostsResponsesRequest()
  {
    using var handler = new RecordingHandler("""{"output_text":"Local reply"}""");
    var client = new OpenAICompatibleResponsesClient(handler);
    var configuration = new LocalLLMEndpointProfile(Guid.NewGuid(), "Test",
        true,
        "http://127.0.0.1:11434/v1",
        ["gpt-oss-20b"],
        "gpt-oss-20b");

    var response = await client.GenerateAssistantContentAsync(
        "Hello",
        [new LocalLLMResponseInput("assistant", "Earlier reply")],
        configuration,
        "test-key",
        TestContext.Current.CancellationToken);

    Assert.Equal("Local reply", response);
    Assert.Equal(HttpMethod.Post, handler.Request?.Method);
    Assert.Equal("http://127.0.0.1:11434/v1/responses", handler.Request?.RequestUri?.ToString());
    Assert.Equal("Bearer", handler.Request?.Headers.Authorization?.Scheme);
    Assert.Equal("test-key", handler.Request?.Headers.Authorization?.Parameter);

    using var document = JsonDocument.Parse(handler.Body);
    Assert.Equal("gpt-oss-20b", document.RootElement.GetProperty("model").GetString());
    var input = document.RootElement.GetProperty("input");
    Assert.Equal("assistant", input[0].GetProperty("role").GetString());
    Assert.Equal("Earlier reply", input[0].GetProperty("content").GetString());
    Assert.Equal("user", input[1].GetProperty("role").GetString());
    Assert.Equal("Hello", input[1].GetProperty("content").GetString());
  }

  [Fact]
  public async Task GenerateAssistantContentAsyncReservesTheEntireBudgetForAnOversizedLatestMessage()
  {
    using var handler = new RecordingHandler("""{"output_text":"Local reply"}""");
    using var client = new OpenAICompatibleResponsesClient(handler);

    await client.GenerateAssistantContentAsync(
        new string('u', 12_500),
        [new("assistant", "history that must not fit")],
        TestProfile(),
        null,
        TestContext.Current.CancellationToken);

    using var document = JsonDocument.Parse(handler.Body);
    var input = document.RootElement.GetProperty("input");
    var latest = Assert.Single(input.EnumerateArray());
    Assert.Equal("user", latest.GetProperty("role").GetString());
    Assert.Equal(12_000, latest.GetProperty("content").GetString()?.Length);
  }

  [Fact]
  public async Task GenerateAssistantContentAsyncRetainsOnlyTheNewestHistoryThatFitsTheAggregateBudget()
  {
    using var handler = new RecordingHandler("""{"output_text":"Local reply"}""");
    using var client = new OpenAICompatibleResponsesClient(handler);

    await client.GenerateAssistantContentAsync(
        new string('u', 1_000),
        [new("assistant", new string('a', 5_000)), new("user", new string('b', 10_000))],
        TestProfile(),
        null,
        TestContext.Current.CancellationToken);

    using var document = JsonDocument.Parse(handler.Body);
    var input = document.RootElement.GetProperty("input").EnumerateArray().ToArray();
    Assert.Equal([1_000, 10_000, 1_000], input.Select(item => item.GetProperty("content").GetString()?.Length));
    Assert.Equal(12_000, input.Sum(item => item.GetProperty("content").GetString()?.Length ?? 0));
  }

  [Fact]
  public async Task GenerateAssistantContentAsyncParsesNestedOutputText()
  {
    using var handler = new RecordingHandler("""
        {
          "output": [
            {
              "content": [
                { "text": "Nested " },
                { "text": "reply" }
              ]
            }
          ]
        }
        """);
    var client = new OpenAICompatibleResponsesClient(handler);
    var configuration = new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true,
        "http://localhost:11434",
        ["local-model"],
        "local-model");

    var response = await client.GenerateAssistantContentAsync(
        "Hello",
        [],
        configuration,
        null,
        TestContext.Current.CancellationToken);

    Assert.Equal("Nested reply", response);
  }

  [Fact]
  public async Task GenerateAssistantContentAsyncThrowsForHttpFailure()
  {
    using var handler = new RecordingHandler("""{"error":"nope"}""", HttpStatusCode.BadGateway);
    var client = new OpenAICompatibleResponsesClient(handler);
    var configuration = new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true,
        "http://localhost:11434",
        ["local-model"],
        "local-model");

    var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GenerateAssistantContentAsync(
        "Hello",
        [],
        configuration,
        null,
        TestContext.Current.CancellationToken));

    Assert.Equal(HttpStatusCode.BadGateway, error.StatusCode);
  }

  [Fact]
  public async Task GenerateAssistantContentAsyncWrapsInvalidJsonResponses()
  {
    using var handler = new RecordingHandler("not json");
    var client = new OpenAICompatibleResponsesClient(handler);
    var configuration = new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true,
        "http://localhost:11434",
        ["local-model"],
        "local-model");

    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateAssistantContentAsync(
        "Hello",
        [],
        configuration,
        null,
        TestContext.Current.CancellationToken));

    Assert.Equal("The local model returned an invalid JSON response.", error.Message);
    Assert.IsType<JsonException>(error.InnerException);
  }

  [Fact]
  public async Task GenerateAssistantContentAsyncValidatesEndpointAndModel()
  {
    using var handler = new RecordingHandler("""{"output_text":"ok"}""");
    var client = new OpenAICompatibleResponsesClient(handler);

    await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateAssistantContentAsync(
        "Hello",
        [],
        new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true, "not a url", ["local-model"], "local-model"),
        null,
        TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateAssistantContentAsync(
        "Hello",
        [],
        new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true, "http://localhost:11434", ["local-model"], " "),
        null,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task RedirectResponseIsNotFollowedOrAllowedToLeakRequestData()
  {
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var firstRequest = ReceiveAndRedirectAsync(listener, port, TestContext.Current.CancellationToken);
    // Goes through the real pinned handler (not a hand-rolled SocketsHttpHandler) so this test stays
    // valid now that the constructor rejects any SocketsHttpHandler that skips the DNS-rebinding
    // guard; CreatePinnedHandler() already sets AllowAutoRedirect = false, which is what this test needs.
    using var client = new OpenAICompatibleResponsesClient(OpenAICompatibleResponsesClient.CreatePinnedHandler());
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true,
        $"http://127.0.0.1:{port}/v1", ["local-model"], "local-model");

    await Assert.ThrowsAsync<LocalLLMRedirectNotAllowedException>(() => client.GenerateAssistantContentAsync(
        "private prompt", [new("assistant", "private history")], profile, "private-token", TestContext.Current.CancellationToken));

    var request = await firstRequest;
    Assert.Contains("Authorization: Bearer private-token", request, StringComparison.Ordinal);
    Assert.Contains("private history", request, StringComparison.Ordinal);
    Assert.Contains("private prompt", request, StringComparison.Ordinal);
    using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listener.AcceptTcpClientAsync(timeout.Token).AsTask());
  }

  [Fact]
  public void CreatePinnedHandlerWiresACleartextBypassProxy()
  {
    using var handler = OpenAICompatibleResponsesClient.CreatePinnedHandler();

    Assert.IsType<OpenAICompatibleResponsesClient.CleartextBypassProxy>(handler.Proxy);
  }

  [Fact]
  public void CleartextBypassProxyBypassesTheInnerProxyOnlyForCleartextRequests()
  {
    var inner = new StubProxy(new Uri("http://proxy.example.test:8080"), isBypassed: false);
    var proxy = new OpenAICompatibleResponsesClient.CleartextBypassProxy(inner);

    Assert.Null(proxy.GetProxy(new Uri("http://models.example.test/v1")));
    Assert.True(proxy.IsBypassed(new Uri("http://models.example.test/v1")));
  }

  [Fact]
  public void CleartextBypassProxyDelegatesHttpsRequestsToTheInnerProxy()
  {
    var inner = new StubProxy(new Uri("http://proxy.example.test:8080"), isBypassed: false);
    var proxy = new OpenAICompatibleResponsesClient.CleartextBypassProxy(inner);

    Assert.Equal(new Uri("http://proxy.example.test:8080"), proxy.GetProxy(new Uri("https://models.example.test/v1")));
    Assert.False(proxy.IsBypassed(new Uri("https://models.example.test/v1")));
  }

  [Fact]
  public void CleartextBypassProxyReadsTheInnerProxysCredentialsUntilOverridden()
  {
    var innerCredentials = new NetworkCredential("inner-user", "inner-pass");
    var inner = new StubProxy(new Uri("http://proxy.example.test:8080"), isBypassed: false)
    {
      Credentials = innerCredentials,
    };
    var proxy = new OpenAICompatibleResponsesClient.CleartextBypassProxy(inner);

    Assert.Same(innerCredentials, proxy.Credentials);
  }

  [Fact]
  public void CleartextBypassProxySetterDoesNotMutateTheSharedInnerProxy()
  {
    // `inner` stands in for HttpClient.DefaultProxy: a static, process-wide instance shared by
    // every HttpClient in the app. Setting Credentials on this wrapper must stay local to it --
    // writing through to `inner` would leak this client's credentials into unrelated HttpClients.
    var inner = new StubProxy(new Uri("http://proxy.example.test:8080"), isBypassed: false);
    var proxy = new OpenAICompatibleResponsesClient.CleartextBypassProxy(inner);
    var credentials = new NetworkCredential("user", "pass");

    proxy.Credentials = credentials;

    Assert.Same(credentials, proxy.Credentials);
    Assert.Null(inner.Credentials);
  }

  [Fact]
  public void RedirectNotAllowedExceptionExposesTheStandardHttpRequestExceptionConstructors()
  {
    // Only the HttpStatusCode constructor above is reachable from production code; the other three
    // exist to satisfy CA1032 (a well-formed exception type exposes all standard constructors) so
    // callers that construct or rethrow this type generically still compile and behave correctly.
    var parameterless = new LocalLLMRedirectNotAllowedException();
    var inner = new InvalidOperationException("inner");
    var withMessage = new LocalLLMRedirectNotAllowedException("redirected");
    var withInner = new LocalLLMRedirectNotAllowedException("redirected", inner);

    Assert.Null(parameterless.InnerException);
    Assert.Equal("redirected", withMessage.Message);
    Assert.Null(withMessage.InnerException);
    Assert.Equal("redirected", withInner.Message);
    Assert.Same(inner, withInner.InnerException);
  }

  private sealed class StubProxy(Uri proxyUri, bool isBypassed) : IWebProxy
  {
    public ICredentials? Credentials { get; set; }

    public Uri GetProxy(Uri destination) => proxyUri;

    public bool IsBypassed(Uri host) => isBypassed;
  }

  private static async Task<string> ReceiveAndRedirectAsync(TcpListener listener, int port, CancellationToken cancellationToken)
  {
    using var client = await listener.AcceptTcpClientAsync(cancellationToken);
    using var stream = client.GetStream();
    using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
    var headers = new StringBuilder(); string? line;
    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken))) headers.AppendLine(line);
    var headerLines = headers.ToString().Split('\n').Select(value => value.Trim()).ToArray();
    var content = headerLines.SingleOrDefault(value => value.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) is { } length
        ? await ReadCharactersAsync(reader, int.Parse(length.Split(':')[1], System.Globalization.CultureInfo.InvariantCulture), cancellationToken)
        : await ReadChunkedAsync(reader, cancellationToken);
    var response = $"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{port}/leak\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
    return headers + Environment.NewLine + content;
  }

  private static LocalLLMEndpointProfile TestProfile() => new(Guid.NewGuid(), "Test", true,
      "http://localhost:11434", ["local-model"], "local-model");

  private static async Task<string> ReadCharactersAsync(StreamReader reader, int length, CancellationToken cancellationToken)
  {
    var content = new char[length]; _ = await reader.ReadBlockAsync(content, cancellationToken); return new string(content);
  }

  private static async Task<string> ReadChunkedAsync(StreamReader reader, CancellationToken cancellationToken)
  {
    var content = new StringBuilder();
    while (await reader.ReadLineAsync(cancellationToken) is { } length && int.Parse(length, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture) is var size && size > 0)
    {
      content.Append(await ReadCharactersAsync(reader, size, cancellationToken));
      _ = await reader.ReadLineAsync(cancellationToken);
    }
    return content.ToString();
  }

  private sealed class RecordingHandler : HttpMessageHandler, IDisposable
  {
    private readonly string responseBody;
    private readonly HttpStatusCode statusCode;

    public RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
      this.responseBody = responseBody;
      this.statusCode = statusCode;
    }

    public HttpRequestMessage? Request { get; private set; }

    public string Body { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Request = request;
      Body = request.Content is null
          ? string.Empty
          : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      return new(statusCode)
      {
        Content = new StringContent(responseBody),
      };
    }
  }
}
