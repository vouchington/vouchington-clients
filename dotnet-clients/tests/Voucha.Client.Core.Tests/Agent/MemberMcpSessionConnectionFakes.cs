using System.Text.Json;
using Voucha.Client.Core.Agent;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  private sealed class FakeFactory(params TaskCompletionSource[] disconnectHolds) : IMemberMcpSessionConnectionFactory
  {
    public List<FakeConnection> Connections { get; } = [];
    public int Created => Connections.Count;
    public bool HoldConnect { get; set; }
    public bool HoldRefresh { get; set; }
    public bool FailConnect { get; set; }
    public bool HoldToolCall { get; set; }
    public bool FailDisconnect { get; set; }
    public Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken)
    {
      var hold = Connections.Count < disconnectHolds.Length ? disconnectHolds[Connections.Count] : null;
      var connection = new FakeConnection(hold, HoldConnect, HoldRefresh, FailConnect, HoldToolCall, FailDisconnect);
      Connections.Add(connection);
      return Task.FromResult<IMemberMcpSessionConnection>(connection);
    }
  }

  private sealed class FakeConnection(TaskCompletionSource? disconnectHold, bool holdConnect, bool holdRefresh,
      bool failConnect, bool holdToolCall, bool failDisconnect)
      : IMemberMcpSessionConnection
  {
    public bool FailRefresh { get; set; }
    public bool UnauthorizedRefresh { get; set; }
    public bool RejectToolCall { get; set; }
    public bool ExpireToolCall { get; set; }
    public int DisconnectCalls { get; private set; }
    public TaskCompletionSource ConnectEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource DisconnectEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource RefreshEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ToolEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<JsonElement> ConnectAndListToolsAsync(CancellationToken cancellationToken)
    {
      ConnectEntered.TrySetResult();
      if (holdConnect) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      if (failConnect) throw new IOException("Transient discovery failure");
      return JsonDocument.Parse("{\"tools\":[]}").RootElement.Clone();
    }
    public async Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken)
    {
      RefreshEntered.TrySetResult();
      if (holdRefresh) await ReleaseRefresh.Task.WaitAsync(cancellationToken);
      if (UnauthorizedRefresh) throw new McpUnauthorizedException();
      if (FailRefresh) throw new IOException("Tool discovery failed");
      return JsonDocument.Parse("{\"tools\":[]}").RootElement.Clone();
    }
    public async Task<McpToolResult> CallToolAsync(string name, JsonElement arguments,
        CancellationToken cancellationToken)
    {
      ToolEntered.TrySetResult();
      if (holdToolCall) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      if (RejectToolCall) throw new McpUnauthorizedException();
      if (ExpireToolCall)
        throw new HttpRequestException("MCP session expired.", null, System.Net.HttpStatusCode.NotFound);
      return new(JsonDocument.Parse("[]").RootElement.Clone(), null, false);
    }
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
      DisconnectCalls++;
      DisconnectEntered.TrySetResult();
      if (disconnectHold is not null) await disconnectHold.Task;
      if (failDisconnect) throw new IOException("Offline remote close");
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
