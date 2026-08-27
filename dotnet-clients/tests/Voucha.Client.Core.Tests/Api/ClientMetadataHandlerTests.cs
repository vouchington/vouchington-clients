using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Tests.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ClientMetadataHandlerTests
{
  [Fact]
  public async Task SendAsyncAddsImmutableMetadataToEveryRequest()
  {
    var terminal = new CaptureHandler();
    var metadata = new ClientMetadata(ClientPlatform.Windows, "2.3.4.56", "1.2.0");
    using var client = new HttpClient(new ClientMetadataHandler(
        metadata,
        new Uri("https://voucha.test"),
        CreateCookieJar(),
        terminal));
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://voucha.test/api/v1/session");
    request.Headers.Add("x-voucha-platform", "ios");

    using var response = await client.SendAsync(
        request,
        HttpCompletionOption.ResponseHeadersRead,
        TestContext.Current.CancellationToken);

    Assert.Equal("dotnet", terminal.Request!.Headers.GetValues("x-voucha-client").Single());
    Assert.Equal("windows", terminal.Request.Headers.GetValues("x-voucha-platform").Single());
    Assert.Equal("2.3.4.56", terminal.Request.Headers.GetValues("x-voucha-app-version").Single());
    Assert.Equal("1.2.0", terminal.Request.Headers.GetValues("x-voucha-sdk-version").Single());
  }

  [Fact]
  public async Task SendAsyncOmitsAbsentSdkVersion()
  {
    var terminal = new CaptureHandler();
    using var client = new HttpClient(new ClientMetadataHandler(
        new ClientMetadata(ClientPlatform.MacOS, "2.3.4"),
        new Uri("https://voucha.test"),
        CreateCookieJar(),
        terminal));

    using var response = await client.GetAsync(
        "https://voucha.test/api/v1/session",
        TestContext.Current.CancellationToken);

    Assert.False(terminal.Request!.Headers.Contains("x-voucha-sdk-version"));
  }

  [Fact]
  public async Task SendAsyncDoesNotExposeMetadataToExternalOrigins()
  {
    var terminal = new CaptureHandler();
    using var client = new HttpClient(new ClientMetadataHandler(
        new ClientMetadata(ClientPlatform.Windows, "2.3.4", "1.2.0"),
        new Uri("https://voucha.test"),
        CreateCookieJar(),
        terminal));

    using var response = await client.GetAsync(
        "https://uploads.example.test/presigned",
        TestContext.Current.CancellationToken);

    Assert.DoesNotContain(terminal.Request!.Headers, header =>
        header.Key.StartsWith("x-voucha-", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public async Task SendAsyncBootstrapsSessionOnceBeforePublicRequests()
  {
    var terminal = new CaptureHandler();
    var jar = CreateCookieJar();
    using var client = new HttpClient(new ClientMetadataHandler(
        new ClientMetadata(ClientPlatform.Windows, "2.3.4", "1.2.0"),
        new Uri("https://voucha.test"),
        jar,
        terminal));

    using var first = await client.GetAsync(
        "https://voucha.test/api/v1/posts",
        TestContext.Current.CancellationToken);
    using var second = await client.GetAsync(
        "https://voucha.test/api/v1/communities",
        TestContext.Current.CancellationToken);

    Assert.Equal(
        ["/api/v1/session", "/api/v1/posts", "/api/v1/communities"],
        terminal.Requests.Select(request => request.RequestUri!.AbsolutePath));
    Assert.All(terminal.Requests, request =>
        Assert.Equal("dotnet", request.Headers.GetValues("x-voucha-client").Single()));
    Assert.Equal("device-token", jar.CookieContainer.GetCookies(new Uri("https://voucha.test"))["dt"]?.Value);
  }

  [Fact]
  public async Task SendAsyncBootstrapsAgainAfterSessionCookiesAreCleared()
  {
    var terminal = new CaptureHandler();
    var jar = CreateCookieJar();
    using var client = new HttpClient(new ClientMetadataHandler(
        new ClientMetadata(ClientPlatform.Windows, "2.3.4", "1.2.0"),
        new Uri("https://voucha.test"),
        jar,
        terminal));

    using var first = await client.GetAsync(
        "https://voucha.test/api/v1/posts",
        TestContext.Current.CancellationToken);
    await jar.ClearAsync(TestContext.Current.CancellationToken);
    using var second = await client.GetAsync(
        "https://voucha.test/api/v1/communities",
        TestContext.Current.CancellationToken);

    Assert.Equal(2, terminal.Requests.Count(request =>
        request.RequestUri!.AbsolutePath == "/api/v1/session"));
  }

  [Fact]
  public async Task SendAsyncCoalescesConcurrentSessionBootstrapRequests()
  {
    var terminal = new CaptureHandler { BootstrapDelay = TimeSpan.FromMilliseconds(50) };
    using var client = new HttpClient(new ClientMetadataHandler(
        new ClientMetadata(ClientPlatform.Windows, "2.3.4", "1.2.0"),
        new Uri("https://voucha.test"),
        CreateCookieJar(),
        terminal));

    var first = client.GetAsync(
        "https://voucha.test/api/v1/posts",
        TestContext.Current.CancellationToken);
    var second = client.GetAsync(
        "https://voucha.test/api/v1/communities",
        TestContext.Current.CancellationToken);
    using var firstResponse = await first;
    using var secondResponse = await second;

    Assert.Equal(1, terminal.Requests.Count(request =>
        request.RequestUri!.AbsolutePath == "/api/v1/session"));
  }

  private static SessionCookieJar CreateCookieJar() => new(
      new CookieContainer(),
      new Uri("https://voucha.test"),
      new TestSessionCookiePersistence());

  private sealed class CaptureHandler : HttpMessageHandler
  {
    public HttpRequestMessage? Request { get; private set; }
    public List<HttpRequestMessage> Requests { get; } = [];
    public TimeSpan BootstrapDelay { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Request = request;
      Requests.Add(request);
      var response = new HttpResponseMessage(HttpStatusCode.OK);
      if (request.RequestUri?.AbsolutePath == "/api/v1/session")
      {
        if (BootstrapDelay > TimeSpan.Zero)
          await Task.Delay(BootstrapDelay, cancellationToken);
        response.Content = new StringContent(
            """{"session":{"dt":"device-token","st":"session-token","dte":2592000,"ste":86400}}""",
            Encoding.UTF8,
            "application/json");
      }
      return response;
    }
  }
}
