using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

/// <summary>
/// Proves the connect-time re-validation gate that closes the DNS-rebinding-shaped gap:
/// <see cref="LocalLLMEndpointProfile"/> only checks the configured hostname when a profile is
/// saved, so these tests drive <see cref="LocalLLMConnectionPinning"/> against synthetic resolved
/// addresses -- never real DNS -- to prove the actually-resolved peer is what gets checked.
/// </summary>
public sealed class LocalLLMConnectionPinningTests
{
  [Fact]
  public void SelectValidatedAddressesRefusesACleartextRequestWhenNoResolvedAddressIsPrivate()
  {
    var error = Assert.Throws<HttpRequestException>(() => LocalLLMConnectionPinning.SelectValidatedAddresses(
        [IPAddress.Parse("8.8.8.8")], new Uri("http://models.example.test/v1/responses"), "models.example.test"));

    Assert.Contains("models.example.test", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void SelectValidatedAddressesRefusesACleartextRequestWhoseResolvedAddressIsRfc6598Cgnat()
  {
    var error = Assert.Throws<HttpRequestException>(() => LocalLLMConnectionPinning.SelectValidatedAddresses(
        [IPAddress.Parse("100.64.0.1")], new Uri("http://models.example.test/v1/responses"), "models.example.test"));

    Assert.Contains("no resolved address is a private/local network address", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void SelectValidatedAddressesAllowsACleartextRequestWhoseResolvedAddressIsPrivate()
  {
    var selected = LocalLLMConnectionPinning.SelectValidatedAddresses(
        [IPAddress.Parse("192.168.1.20")], new Uri("http://models.example.test/v1/responses"), "models.example.test");

    Assert.Equal([IPAddress.Parse("192.168.1.20")], selected);
  }

  [Fact]
  public void SelectValidatedAddressesFiltersOutPublicCandidatesButKeepsEveryPrivateOne()
  {
    var selected = LocalLLMConnectionPinning.SelectValidatedAddresses(
        [IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.5"), IPAddress.Parse("192.168.1.1")],
        new Uri("http://models.example.test/v1/responses"),
        "models.example.test");

    Assert.Equal([IPAddress.Parse("10.0.0.5"), IPAddress.Parse("192.168.1.1")], selected);
  }

  [Fact]
  public void SelectValidatedAddressesDoesNotReCheckHttpsRequests()
  {
    var selected = LocalLLMConnectionPinning.SelectValidatedAddresses(
        [IPAddress.Parse("8.8.8.8")], new Uri("https://models.example.test/v1/responses"), "models.example.test");

    Assert.Equal([IPAddress.Parse("8.8.8.8")], selected);
  }

  [Fact]
  public void SelectValidatedAddressesThrowsWhenNoAddressWasResolved()
  {
    var error = Assert.Throws<HttpRequestException>(() => LocalLLMConnectionPinning.SelectValidatedAddresses(
        [], new Uri("http://models.example.test/v1/responses"), "models.example.test"));

    Assert.Contains("no usable addresses", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void SelectValidatedAddressesFailsClosedWhenTheRequestUriIsNull()
  {
    // A null request URI must never fall through to "no filtering applied" -- this internal helper
    // is testable in isolation from ConnectAsync (which always supplies a real request URI), so a
    // future caller passing null must still be refused rather than silently bypassing the gate.
    var error = Assert.Throws<HttpRequestException>(() => LocalLLMConnectionPinning.SelectValidatedAddresses(
        [IPAddress.Parse("192.168.1.20")], null, "models.example.test"));

    Assert.Contains("models.example.test", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ConnectValidatedAsyncRefusesToConnectWhenTheResolvedAddressIsNotPrivate()
  {
    var error = await Assert.ThrowsAsync<HttpRequestException>(() => LocalLLMConnectionPinning.ConnectValidatedAsync(
        [IPAddress.Parse("8.8.8.8")],
        new Uri("http://models.example.test/v1/responses"),
        new DnsEndPoint("models.example.test", 0),
        TestContext.Current.CancellationToken).AsTask());

    Assert.Contains("models.example.test", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ConnectValidatedAsyncConnectsToThePrivateResolvedAddressOverARealSocket()
  {
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var acceptTask = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);

    await using var stream = await LocalLLMConnectionPinning.ConnectValidatedAsync(
        [IPAddress.Loopback],
        new Uri("http://models.example.test/v1/responses"),
        new DnsEndPoint("models.example.test", port),
        TestContext.Current.CancellationToken);

    using var accepted = await acceptTask;
    Assert.True(accepted.Connected);
    Assert.True(stream.CanWrite);
  }

  [Fact]
  public async Task ConnectValidatedAsyncFallsBackToTheNextPrivateAddressWhenAnEarlierOneRefusesTheConnection()
  {
    // IPv4 and IPv6 loopback have independent port namespaces, so listening only on ::1 leaves
    // 127.0.0.1 unlistened on the same port -- proving a refused first candidate does not abort the
    // whole resolved-address list, matching the multi-address resilience the default
    // SocketsHttpHandler connect logic has (e.g. a dual-stack "localhost" lookup).
    using var listener = new TcpListener(IPAddress.IPv6Loopback, 0);
    // Without this, a dual-stack OS can also bind 127.0.0.1 (mapped) on the same port, so the first
    // candidate would not actually be refused and the test would not prove fallback.
    listener.Server.DualMode = false;
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var acceptTask = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);

    await using var stream = await LocalLLMConnectionPinning.ConnectValidatedAsync(
        [IPAddress.Loopback, IPAddress.IPv6Loopback],
        new Uri("http://models.example.test/v1/responses"),
        new DnsEndPoint("models.example.test", port),
        TestContext.Current.CancellationToken);

    using var accepted = await acceptTask;
    Assert.True(accepted.Connected);
  }

  [Fact]
  public async Task ConnectValidatedAsyncConnectsAnIPv4MappedLoopbackAddressOverARealIPv4Socket()
  {
    // "::ffff:127.0.0.1" is a valid resolved candidate the endpoint policy already accepts as
    // private (see LocalLLMEndpointProfileTests). An explicitly constructed IPv6 socket is
    // IPv6-only on some platforms (notably Windows) unless dual-mode is enabled, so connecting it
    // straight to an IPv4-mapped address can fail there even though the real target -- 127.0.0.1 --
    // is reachable. Proves TryConnectAsync maps the address down to IPv4 before choosing the
    // socket's address family instead of relying on platform dual-mode behavior.
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var acceptTask = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);

    await using var stream = await LocalLLMConnectionPinning.ConnectValidatedAsync(
        [IPAddress.Parse("::ffff:127.0.0.1")],
        new Uri("http://models.example.test/v1/responses"),
        new DnsEndPoint("models.example.test", port),
        TestContext.Current.CancellationToken);

    using var accepted = await acceptTask;
    Assert.True(accepted.Connected);
  }

  [Fact]
  public async Task ConnectAsyncIsWiredThroughAsTheHandlersRealConnectCallback()
  {
    // SocketsHttpConnectionContext has no public constructor, so ConnectAsync (the actual
    // SocketsHttpHandler.ConnectCallback delegate) can only be exercised end-to-end through a real
    // HttpClient request, not by calling it directly with a hand-built context.
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var serverTask = RespondWithJsonAsync(listener, TestContext.Current.CancellationToken);

    using var client = new OpenAICompatibleResponsesClient();
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true,
        $"http://127.0.0.1:{port}/v1", ["local-model"], "local-model");

    var response = await client.GenerateAssistantContentAsync(
        "Hello", [], profile, null, TestContext.Current.CancellationToken);

    Assert.Equal("Local reply", response);
    await serverTask;
  }

  [Fact]
  public async Task ConnectValidatedAsyncThrowsWhenEveryResolvedCandidateRefusesTheConnection()
  {
    var port = ReserveAnUnusedLoopbackPort();

    var error = await Assert.ThrowsAsync<HttpRequestException>(() => LocalLLMConnectionPinning.ConnectValidatedAsync(
        [IPAddress.Loopback, IPAddress.IPv6Loopback],
        new Uri("http://models.example.test/v1/responses"),
        new DnsEndPoint("models.example.test", port),
        TestContext.Current.CancellationToken).AsTask());

    Assert.Contains("Unable to connect to any resolved address", error.Message, StringComparison.Ordinal);
    Assert.IsType<SocketException>(error.InnerException);
  }

  [Fact]
  public async Task ConnectValidatedAsyncPropagatesAnExternalCancellationInsteadOfSwallowingItAsACandidateFailure()
  {
    // A per-candidate timeout firing is swallowed so the next candidate gets a turn (see the
    // fall-back test above). A cancellation the *caller* requested is a different signal entirely --
    // it must propagate immediately instead of being treated as "this candidate failed, try the next
    // one", which is exactly what distinguishes the two catch clauses in TryConnectAsync.
    var port = ReserveAnUnusedLoopbackPort();
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LocalLLMConnectionPinning.ConnectValidatedAsync(
        [IPAddress.Loopback, IPAddress.IPv6Loopback],
        new Uri("http://models.example.test/v1/responses"),
        new DnsEndPoint("models.example.test", port),
        cts.Token).AsTask());
  }

  private static int ReserveAnUnusedLoopbackPort()
  {
    using var probe = new TcpListener(IPAddress.Loopback, 0);
    probe.Start();
    var port = ((IPEndPoint)probe.LocalEndpoint).Port;
    probe.Stop();
    return port;
  }

  private static async Task RespondWithJsonAsync(TcpListener listener, CancellationToken cancellationToken)
  {
    using var accepted = await listener.AcceptTcpClientAsync(cancellationToken);
    using var stream = accepted.GetStream();
    using var reader = new StreamReader(
        stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
    var contentLength = 0;
    string? line;
    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken)))
    {
      if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
      {
        contentLength = int.Parse(line.Split(':')[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
      }
    }
    if (contentLength > 0)
    {
      var body = new char[contentLength];
      _ = await reader.ReadBlockAsync(body, cancellationToken);
    }

    const string responseBody = """{"output_text":"Local reply"}""";
    var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
        $"{Encoding.ASCII.GetByteCount(responseBody)}\r\nConnection: close\r\n\r\n{responseBody}";
    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
  }
}
