using System.Text.Json;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ChatMemberMcpControlsTests
{
  [Fact]
  public async Task MemberChatShowsConnectThenLiveRefreshAndDisconnectControls()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
        ["Eyebrow"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["native.taxonomy.settings.mcp"] = "MCP",
        ["native.dotnet.chat.mcpConnect"] = "Connect",
        ["native.dotnet.chat.mcpConnected"] = "Connected",
        ["native.dotnet.chat.mcpConnectionFailed"] = "Connection failed",
        ["native.dotnet.chat.mcpRefresh"] = "Refresh",
        ["native.dotnet.chat.mcpDisconnect"] = "Disconnect",
      },
    };
    using var session = new MemberMcpSession(new MemberSession(), new EmptyStore(), new ConnectionFactory());
    var service = new UnusedChatService();
    var page = new ChatConversationPage(new ChatConversationViewModel(service), service, session);
    var view = Assert.Single(page.GetVisualTreeDescendants().OfType<ChatMemberMcpControlsView>());
    typeof(ChatMemberMcpControlsView).GetMethod("OnLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(view, [view, EventArgs.Empty]);
    var buttons = view.GetVisualTreeDescendants().OfType<Button>().ToArray();
    Assert.Equal(3, buttons.Length);
    Assert.True(Assert.Single(buttons, button => button.Text == "Connect").IsVisible);

    await session.ConnectAsync(TestContext.Current.CancellationToken);

    Assert.False(Assert.Single(buttons, button => button.Text == "Connect").IsVisible);
    Assert.True(Assert.Single(buttons, button => button.Text == "Refresh").IsVisible);
    Assert.True(Assert.Single(buttons, button => button.Text == "Disconnect").IsVisible);
    Assert.Equal(1, session.Tools?.GetProperty("tools").GetArrayLength());
  }

  [Fact]
  public async Task MemberChatShowsDisconnectWhileBrowserAuthorizationIsPending()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
        ["Eyebrow"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["native.taxonomy.settings.mcp"] = "MCP",
        ["native.dotnet.chat.mcpConnect"] = "Connect",
        ["native.dotnet.chat.mcpConnected"] = "Connected",
        ["native.dotnet.chat.mcpConnectionFailed"] = "Connection failed",
        ["native.dotnet.chat.mcpRefresh"] = "Refresh",
        ["native.dotnet.chat.mcpDisconnect"] = "Disconnect",
      },
    };
    var connection = new PendingConnection();
    using var session = new MemberMcpSession(new MemberSession(), new EmptyStore(),
        new PendingConnectionFactory(connection));
    var service = new UnusedChatService();
    var page = new ChatConversationPage(new ChatConversationViewModel(service), service, session);
    var view = Assert.Single(page.GetVisualTreeDescendants().OfType<ChatMemberMcpControlsView>());
    typeof(ChatMemberMcpControlsView).GetMethod("OnLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(view, [view, EventArgs.Empty]);
    var buttons = view.GetVisualTreeDescendants().OfType<Button>().ToArray();

    var connecting = session.ConnectAsync(TestContext.Current.CancellationToken);
    await connection.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.False(Assert.Single(buttons, button => button.Text == "Connect").IsVisible);
    Assert.True(Assert.Single(buttons, button => button.Text == "Disconnect").IsVisible);
    await session.DisconnectAsync();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting);
    Assert.True(Assert.Single(buttons, button => button.Text == "Connect").IsVisible);
  }

  private sealed class PendingConnectionFactory(PendingConnection connection) : IMemberMcpSessionConnectionFactory
  {
    public Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IMemberMcpSessionConnection>(connection);
  }

  private sealed class PendingConnection : IMemberMcpSessionConnection
  {
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<JsonElement> ConnectAndListToolsAsync(CancellationToken cancellationToken)
    {
      Entered.TrySetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      throw new InvalidOperationException("The callback wait was not canceled.");
    }
    public Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  private sealed class MemberSession : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("member-1", "member"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class EmptyStore : IMemberMcpAccountTokenStore
  {
    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope) => Task.FromResult<MemberMcpOAuthTokens?>(null);
    public Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens) => Task.CompletedTask;
    public Task ClearAsync(MemberMcpOAuthTokenScope scope) => Task.CompletedTask;
    public Task SavePendingAsync(string accountId, string state, string verifier, Uri redirectUri) => Task.CompletedTask;
    public Task ClearPendingAsync(string accountId) => Task.CompletedTask;
    public Task<bool> HasAccountGrantAsync(string accountId) => Task.FromResult(false);
    public Task<IReadOnlyList<MemberMcpStoredGrant>> TakeAccountAsync(string accountId) =>
        Task.FromResult<IReadOnlyList<MemberMcpStoredGrant>>([]);
    public Task ClearAccountAsync(string accountId) => Task.CompletedTask;
    public Task ClearAllAccountsAsync() => Task.CompletedTask;
    public Task ClearAllAccountsExceptAsync(string? accountId) => Task.CompletedTask;
  }

  private sealed class ConnectionFactory : IMemberMcpSessionConnectionFactory
  {
    public Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IMemberMcpSessionConnection>(new Connection());
  }

  private sealed class Connection : IMemberMcpSessionConnection
  {
    public Task<JsonElement> ConnectAndListToolsAsync(CancellationToken cancellationToken) => ListToolsAsync(cancellationToken);
    public Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken) => Task.FromResult(
        JsonDocument.Parse("{\"tools\":[{\"name\":\"read_profile\"}]}").RootElement.Clone());
    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }

  private sealed class UnusedChatService : IChatService
  {
    public Task<ChatConversationListResponse> FetchMyConversationsAsync(string? after = null, int limit = 50,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ChatConversationResponse> CreateConversationAsync(CreateChatConversationBody body,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ChatMessagesResponse> FetchConversationMessagesAsync(string conversationId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ChatConversationResponse> UpdateConversationTitleAsync(string conversationId, string title,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ChatConversationResponse> GenerateConversationTitleAsync(string conversationId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteConversationAsync(string conversationId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ClientGeneratedChatResponse> CreateClientGeneratedChatAsync(string conversationId,
        CreateClientGeneratedChatBody body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}
