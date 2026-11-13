using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Tests.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Theory]
  [InlineData("pending")]
  [InlineData("failure")]
  [InlineData("network")]
  [InlineData("connected")]
  public async Task OAuthCompletionWithoutCredentialChangesPreservesRealMemberObserversAndStoredGrants(string outcome)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    using var chat = new ChatConversationViewModel(new FakeChatService());
    using var member = new ChatConversationMemberSession(fixture.Store, chat, action => action());
    member.Attach();
    await chat.LoadAsync("conversation-1", "Retained", TestContext.Current.CancellationToken);
    using var grants = new ProtectedMemberMcpTokenStore(new Round2ProtectedValues());
    var scope = Round2GrantScope();
    await grants.SaveAsync(scope, new("retained-access", "refresh", 3600, "mcp.user:read", "Bearer"));
    var connection = new Round2McpConnection();
    using var mcp = new MemberMcpSession(fixture.Store, grants, new Round2McpFactory(connection));
    await mcp.ConnectAsync(TestContext.Current.CancellationToken);
    mcp.RequestWriteAccess = true;
    var snapshots = new List<SessionSnapshot>();
    fixture.Store.SessionChanged += (_, args) => snapshots.Add(args.Snapshot);
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Terminal.Other = async _ =>
    {
      entered.TrySetResult();
      await release.Task.WaitAsync(TestContext.Current.CancellationToken);
      if (outcome == "network") throw new HttpRequestException("offline");
      return outcome switch
      {
        "failure" => Json(HttpStatusCode.BadRequest, new { code = "OAUTH_FAILED" }),
        "connected" => Json(HttpStatusCode.OK, new { oauth_account = new { id = "provider", name = "Alice" } }),
        _ => Json(HttpStatusCode.Accepted, new { status = "pending" }),
      };
    };
    var generation = fixture.Jar.Generation;
    var complete = fixture.Client.CompleteOAuthAuthorizationAsync("flow", "completion", "proof",
        TestContext.Current.CancellationToken);
    try
    {
      await Task.WhenAny(entered.Task, complete).WaitAsync(TestContext.Current.CancellationToken);
      if (complete.IsCompleted) await complete;
      await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.True(fixture.Jar.Generation > generation);
      Assert.Equal("user-1", fixture.Store.Current.Identity?.Id);
      Assert.Equal("conversation-1", chat.ConversationId);
      Assert.Equal(MemberMcpSessionStatus.Connected, mcp.Status);
      Assert.Equal("retained-access", (await grants.LoadAsync(scope))?.AccessToken);
    }
    finally { release.TrySetResult(); }
    if (outcome is "failure" or "network") await Assert.ThrowsAnyAsync<HttpRequestException>(() => complete);
    else Assert.Equal(outcome == "pending" ? OAuthCompletionKind.Pending : OAuthCompletionKind.Connected, (await complete).Kind);
    await mcp.WhenSettledAsync();
    Assert.Empty(snapshots);
    Assert.Equal("user-1", member.CurrentMemberId);
    Assert.Equal("conversation-1", chat.ConversationId);
    Assert.Equal(MemberMcpSessionStatus.Connected, mcp.Status);
    Assert.True(mcp.RequestWriteAccess);
    Assert.NotNull(mcp.Tools);
    Assert.Equal(0, connection.DisconnectCalls);
    Assert.Equal("retained-access", (await grants.LoadAsync(scope))?.AccessToken);
    await mcp.DisconnectAsync();
  }

  [Theory]
  [InlineData("signout")]
  [InlineData("cookies")]
  [InlineData("raw-logout")]
  public async Task ActualClearOrCommittedAuthCookieChangeStillNotifiesAndCleansMemberState(string action)
  {
    using var fixture = new RecoveryFixture();
    await fixture.SeedAsync();
    await ((ISessionStore)fixture.Store).RefreshAsync(TestContext.Current.CancellationToken);
    using var chat = new ChatConversationViewModel(new FakeChatService());
    using var member = new ChatConversationMemberSession(fixture.Store, chat, action => action());
    member.Attach();
    await chat.LoadAsync("conversation-1", "Reset", TestContext.Current.CancellationToken);
    using var grants = new ProtectedMemberMcpTokenStore(new Round2ProtectedValues());
    var scope = Round2GrantScope();
    await grants.SaveAsync(scope, new("removed-access", "refresh", 3600, "mcp.user:read", "Bearer"));
    var connection = new Round2McpConnection();
    using var mcp = new MemberMcpSession(fixture.Store, grants, new Round2McpFactory(connection));
    await mcp.ConnectAsync(TestContext.Current.CancellationToken);
    fixture.Terminal.Other = _ => Task.FromResult(action == "cookies"
        ? WithCookies("changed-dt", "changed-st") : Json(HttpStatusCode.OK, new { }));
    if (action == "cookies")
    {
      await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Post, "/api/v1/auth/email-address/login"),
          TestContext.Current.CancellationToken);
    }
    else if (action == "raw-logout")
      await fixture.Client.SendAsync(VouchaApiEndpoints.Logout(), TestContext.Current.CancellationToken);
    else await fixture.Store.SignOutAsync(TestContext.Current.CancellationToken);
    await mcp.WhenSettledAsync();
    Assert.False(fixture.Store.Current.IsAuthenticated);
    Assert.Null(chat.ConversationId);
    Assert.Equal(MemberMcpSessionStatus.Disconnected, mcp.Status);
    Assert.Null(mcp.Tools);
    Assert.Null(await grants.LoadAsync(scope));
    Assert.Equal(1, connection.DisconnectCalls);
  }

  private static MemberMcpOAuthTokenScope Round2GrantScope() => new("user-1", "https://issuer.test/",
      new Uri("https://voucha.test/api/v1/mcp"), new Uri("https://voucha.test/native-client"));
  private sealed class Round2ProtectedValues : IMemberMcpProtectedValues
  {
    private readonly Dictionary<string, string> values = [];
    public Task<string?> ReadAsync(string key) => Task.FromResult(values.GetValueOrDefault(key));
    public Task WriteAsync(string key, string value) { values[key] = value; return Task.CompletedTask; }
    public Task RemoveAsync(string key) { values.Remove(key); return Task.CompletedTask; }
  }
  private sealed class Round2McpFactory(Round2McpConnection connection) : IMemberMcpSessionConnectionFactory
  {
    public Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IMemberMcpSessionConnection>(connection);
  }
  private sealed class Round2McpConnection : IMemberMcpSessionConnection
  {
    public int DisconnectCalls { get; private set; }
    public Task<JsonElement> ConnectAndListToolsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(JsonDocument.Parse("{\"tools\":[]}").RootElement.Clone());
    public Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken) => ConnectAndListToolsAsync(cancellationToken);
    public Task<McpToolResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task DisconnectAsync(CancellationToken cancellationToken) { DisconnectCalls++; return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
