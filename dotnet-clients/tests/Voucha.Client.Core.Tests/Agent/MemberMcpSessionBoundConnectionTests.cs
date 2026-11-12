using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Agent.MemberMcpSessionTokenSourceTests;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpSessionBoundConnectionTests
{
  private static readonly Uri Site = new("https://api.test");

  [Fact]
  public async Task ConnectListsTheServerCatalogWithTheMintedBearerAndNoConsent()
  {
    var mint = new MintHandler();
    var mcp = new McpHandler();
    await using var connection = Connection(mint, mcp);

    var catalog = await connection.ConnectAndListToolsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("server_tool", catalog.GetProperty("tools")[0].GetProperty("name").GetString());
    Assert.All(mcp.Bearers, bearer => Assert.Equal("Bearer token-1", bearer));
    Assert.Single(mint.Requests);
  }

  [Fact]
  public async Task AuthorizeIgnoresTheRequestedScopeAndNeverOpensABrowser()
  {
    var mint = new MintHandler();
    await using var connection = Connection(mint, new McpHandler());

    await connection.AuthorizeAndListToolsAsync("mcp.user:write", TestContext.Current.CancellationToken);

    Assert.DoesNotContain("mcp.user:write", Assert.Single(mint.Requests).Body, StringComparison.Ordinal);
  }

  [Fact]
  public async Task UnauthorizedCallMintsOnceMoreThenSucceeds()
  {
    var mint = new MintHandler();
    var mcp = new McpHandler { RejectFirstToken = true };
    await using var connection = Connection(mint, mcp);

    await connection.ListToolsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, mint.Requests.Count);
    Assert.Equal("Bearer token-2", mcp.Bearers[^1]);
  }

  [Fact]
  public async Task SecondUnauthorizedSurfacesInsteadOfLooping()
  {
    var mint = new MintHandler();
    await using var connection = Connection(mint, new McpHandler { RejectAll = true });

    await Assert.ThrowsAsync<McpUnauthorizedException>(
        () => connection.ListToolsAsync(TestContext.Current.CancellationToken));

    Assert.Equal(2, mint.Requests.Count);
  }

  [Fact]
  public async Task DisconnectClosesWithTheLiveTokenAndNeverMintsAgain()
  {
    var mint = new MintHandler();
    var mcp = new McpHandler();
    await using var connection = Connection(mint, mcp);
    await connection.ListToolsAsync(TestContext.Current.CancellationToken);

    await connection.DisconnectAsync(TestContext.Current.CancellationToken);
    await connection.DisconnectAsync(TestContext.Current.CancellationToken);

    Assert.Contains("DELETE", mcp.Methods);
    Assert.Equal("Bearer token-1", Assert.Single(mcp.DeleteBearers));
    Assert.Single(mint.Requests);
    await Assert.ThrowsAsync<McpUnauthorizedException>(
        () => connection.ListToolsAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task DisconnectWithoutAMintedTokenDoesNotMint()
  {
    var mint = new MintHandler();
    await using var connection = Connection(mint, new McpHandler());

    await connection.DisconnectAsync(TestContext.Current.CancellationToken);

    Assert.Empty(mint.Requests);
  }

  [Fact]
  public async Task FactoryBindsTokensToTheAccountAndMcpResource()
  {
    var account = "account-a";
    var mint = new MintHandler();
    var api = new VouchaApiClient(new HttpClient(mint) { BaseAddress = Site });
    var factory = new MemberMcpSessionBoundConnectionFactory(
        Site, api, new FixedSession(() => account), new NoLegacyFactory(), () => new McpHandler());
    await using var connection = await factory.CreateAsync("account-a", TestContext.Current.CancellationToken);
    await connection.ListToolsAsync(TestContext.Current.CancellationToken);

    account = "account-b";

    await Assert.ThrowsAsync<McpUnauthorizedException>(
        () => connection.CallToolAsync("server_tool", JsonDocument.Parse("{}").RootElement,
            TestContext.Current.CancellationToken));
  }

  [Fact]
  public void FactoryRequiresALegacyGrantRevocationFactory()
  {
    var api = new VouchaApiClient(new HttpClient(new MintHandler()) { BaseAddress = Site });

    Assert.Throws<ArgumentNullException>(() => new MemberMcpSessionBoundConnectionFactory(
        Site, api, new FixedSession(() => "account-a"), null!));
    var legacy = new NoLegacyFactory();
    Assert.Same(legacy, new MemberMcpSessionBoundConnectionFactory(
        Site, api, new FixedSession(() => "account-a"), legacy).LegacyGrantRevocationFactory);
  }

  private sealed class NoLegacyFactory : IMemberMcpSessionConnectionFactory
  {
    public Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
  }

  private static MemberMcpSessionBoundConnection Connection(MintHandler mint, McpHandler mcp) =>
      new(new MemberMcpClient(Site, mcp),
          new MemberMcpSessionTokenSource(
              new VouchaApiClient(new HttpClient(mint) { BaseAddress = Site }),
              new Uri(Site, "/api/v1/mcp"), "windows"));

  private sealed class FixedSession(Func<string> account) : Voucha.Client.Core.Auth.ISessionStore
  {
    public event EventHandler<Voucha.Client.Core.Auth.SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public Voucha.Client.Core.Auth.SessionSnapshot Current =>
        new(new User(account(), "member"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class McpHandler : HttpMessageHandler
  {
    public List<string?> Bearers { get; } = [];
    public List<string> Methods { get; } = [];
    public List<string?> DeleteBearers { get; } = [];
    public bool RejectFirstToken { get; init; }
    public bool RejectAll { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.Method == HttpMethod.Delete)
      {
        Methods.Add("DELETE");
        DeleteBearers.Add(request.Headers.Authorization?.ToString());
        return new HttpResponseMessage(HttpStatusCode.NoContent);
      }
      var bearer = request.Headers.Authorization?.ToString();
      Bearers.Add(bearer);
      using var rpc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
      var method = rpc.RootElement.GetProperty("method").GetString();
      if (method != "notifications/initialized" && (RejectAll || RejectFirstToken && bearer == "Bearer token-1"))
        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
      if (method == "notifications/initialized") return new HttpResponseMessage(HttpStatusCode.Accepted);
      var id = rpc.RootElement.GetProperty("id").GetString();
      var result = method == "initialize"
          ? """{"protocolVersion":"2025-06-18","capabilities":{"tools":{}},"serverInfo":{"name":"t","version":"1"}}"""
          : """{"tools":[{"name":"server_tool","inputSchema":{"type":"object"}}]}""";
      var response = new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent($$"""{"jsonrpc":"2.0","id":"{{id}}","result":{{result}}}""",
            Encoding.UTF8, "application/json"),
      };
      if (method == "initialize") response.Headers.TryAddWithoutValidation("Mcp-Session-Id", "s");
      return response;
    }
  }
}
