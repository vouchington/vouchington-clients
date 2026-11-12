using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class SessionRecoveryMutationChallengeTests
{
  private static readonly Uri Origin = new("https://voucha.test");

  [Theory]
  [InlineData(true, true)]
  [InlineData(true, false)]
  [InlineData(false, true)]
  [InlineData(false, false)]
  public async Task MutationRecoveryAndChallengeComposeInEitherOrder(bool renewalFirst, bool typed)
  {
    var persistence = new TestSessionCookiePersistence();
    using var jar = new SessionCookieJar(new CookieContainer(), Origin, persistence);
    await jar.StoreSessionTokensAsync("old-dt", "old-st", TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
    var terminal = new InterleavedTerminal(renewalFirst, persistence);
    using var http = new HttpClient(new ClientMetadataHandler(new(ClientPlatform.Windows, "1.2"), Origin, jar,
        new SessionCookiePersistenceHandler(jar, terminal)))
    { BaseAddress = Origin };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("NativeTests/1.2");
    var prompts = 0;
    var body = new Dictionary<string, string> { ["value"] = "original" };
    CookieSessionStore? store = null;
    var client = new VouchaApiClient(http, new BotChallengeReplay(() => store!, (action, _) =>
    {
      Assert.Equal("bot-challenge.mutation", action);
      prompts++;
      body["value"] = "changed-after-dispatch";
      return Task.FromResult("fresh-proof");
    }));
    store = new CookieSessionStore(client, jar);
    var request = new ApiRequest(HttpMethod.Post, "/api/v1/items/item")
    {
      Query = new Dictionary<string, string> { ["query"] = "two words" },
      Headers = new Dictionary<string, string> { ["Idempotency-Key"] = "same-operation" },
      Body = body,
    };

    if (typed)
    {
      var result = await client.SendAsync<MutationResult>(request, TestContext.Current.CancellationToken);
      Assert.True(result.Success);
    }
    else await client.SendAsync(request, TestContext.Current.CancellationToken);

    var captured = terminal.Captured;
    Assert.Equal(1, prompts);
    Assert.Equal(4, captured.Length);
    Assert.Equal(renewalFirst
        ? ["/api/v1/items/item", "/api/v1/session", "/api/v1/items/item", "/api/v1/items/item"]
        : new[] { "/api/v1/items/item", "/api/v1/items/item", "/api/v1/session", "/api/v1/items/item" },
        captured.Select(value => value.Path));
    var mutations = captured.Where(value => value.Path == "/api/v1/items/item").ToArray();
    Assert.Equal(3, mutations.Length);
    Assert.All(mutations, value =>
    {
      Assert.Equal("POST", value.Method);
      Assert.Equal("?query=two%20words", value.Query);
      Assert.Equal("same-operation", value.IdempotencyKey);
      Assert.Equal(mutations[0].Body, value.Body);
      using var document = JsonDocument.Parse(value.Body!);
      Assert.Equal("original", document.RootElement.GetProperty("value").GetString());
    });
    Assert.Null(mutations[0].Proof);
    Assert.Equal(renewalFirst ? null : "fresh-proof", mutations[1].Proof);
    Assert.Equal("fresh-proof", mutations[2].Proof);
    Assert.Contains("st=new-st", mutations[2].Cookies!);
    Assert.All(captured, value =>
    {
      Assert.Equal("dotnet", value.Client);
      Assert.Equal("windows", value.Platform);
      Assert.Equal("1.2", value.AppVersion);
      Assert.Equal("NativeTests/1.2", value.UserAgent);
    });
    Assert.Equal("new-st", jar.CookieContainer.GetCookies(Origin)["st"]!.Value);
    Assert.Contains("new-st", persistence.Value!);
    Assert.DoesNotContain("old-st", persistence.Value!);
  }

  private sealed record MutationResult(bool Success);
  private sealed record CapturedRequest(string Path, string Query, string Method, string? Body, string? Cookies,
      string? Proof, string? IdempotencyKey, string? Client, string? Platform, string? AppVersion, string? UserAgent);

  private sealed class InterleavedTerminal(bool renewalFirst, TestSessionCookiePersistence persistence) : HttpMessageHandler
  {
    private readonly object gate = new();
    private readonly List<CapturedRequest> requests = [];
    public CapturedRequest[] Captured { get { lock (gate) return requests.ToArray(); } }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
      var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
      var cookies = Header(request, "Cookie");
      var proof = Header(request, BotChallengeReplay.TokenHeader);
      lock (gate) requests.Add(new(path, request.RequestUri.Query, request.Method.Method, body, cookies, proof,
          Header(request, "Idempotency-Key"), Header(request, "x-voucha-client"), Header(request, "x-voucha-platform"),
          Header(request, "x-voucha-app-version"), Header(request, "User-Agent")));
      if (path == "/api/v1/session")
      {
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Null(proof);
        using var document = JsonDocument.Parse(body!);
        Assert.Equal("old-dt", document.RootElement.GetProperty("dt").GetString());
        Assert.Equal("old-st", document.RootElement.GetProperty("st").GetString());
        return Response(request, HttpStatusCode.OK, new
        {
          session = new
          {
            dt = "new-dt",
            st = "new-st",
            dte = 120,
            ste = 60,
            secure = true,
            uid = "member",
            session = new { uid = "member", client = "web" },
          },
        });
      }
      Assert.Equal("/api/v1/items/item", path);
      var renewed = cookies?.Contains("st=new-st", StringComparison.Ordinal) == true;
      if (renewalFirst && !renewed || !renewalFirst && proof is not null && !renewed)
        return Response(request, HttpStatusCode.Unauthorized, new { code = "AUTH_REQUIRED" });
      if (proof is null)
        return new(HttpStatusCode.Forbidden)
        {
          RequestMessage = request,
          Content = new StringContent(ApiFixtureLoader.LoadResponse("native.bot-challenge.required")),
        };
      Assert.Equal("fresh-proof", proof);
      Assert.True(renewed);
      Assert.Contains("new-st", persistence.Value!);
      return Response(request, HttpStatusCode.OK, new { success = true });
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? Assert.Single(values) : null;
    private static HttpResponseMessage Response(HttpRequestMessage request, HttpStatusCode status, object body) =>
        new(status) { RequestMessage = request, Content = JsonContent.Create(body) };
  }
}
