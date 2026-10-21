using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Agent;

/// <summary>Presentation state for the reachable Windows chat MCP connection controls.</summary>
public sealed class MemberMcpControlsViewModel(MemberMcpSession session) : ObservableObject
{
  private bool hasError;
  public bool RequestWriteAccess
  {
    get => session.RequestWriteAccess;
    set
    {
      session.RequestWriteAccess = value;
      OnPropertyChanged();
    }
  }
  public bool CanAuthorize => HasMemberSession && !IsBusy;

  public bool HasMemberSession => session.HasMemberSession;
  public bool IsConnected => session.Status == MemberMcpSessionStatus.Connected;
  public bool IsBusy => session.Status == MemberMcpSessionStatus.Connecting;
  public bool CanConnect => HasMemberSession && !IsConnected && !IsBusy &&
      (!hasError || session.Status == MemberMcpSessionStatus.Error);
  public bool CanDisconnect => IsBusy || IsConnected || HasError;
  public bool HasError => hasError || session.Status == MemberMcpSessionStatus.Error;
  public IReadOnlyList<UiText> ToolNames => session.Tools is { ValueKind: System.Text.Json.JsonValueKind.Object } catalog &&
      catalog.TryGetProperty("tools", out var tools) && tools.ValueKind == System.Text.Json.JsonValueKind.Array
          ? tools.EnumerateArray().Where(tool => tool.ValueKind == System.Text.Json.JsonValueKind.Object &&
              tool.TryGetProperty("name", out var name) && name.ValueKind == System.Text.Json.JsonValueKind.String)
              .Select(tool => UiText.ProtocolValue(tool.GetProperty("name").GetString())).ToArray()
          : [];

  public async Task ConnectAsync(CancellationToken cancellationToken = default)
  {
    hasError = false;
    Refresh();
    var scope = RequestWriteAccess ? "mcp.user:read mcp.user:write" : "mcp.user:read";
    if (IsConnected) await session.AuthorizeAsync(scope, cancellationToken).ConfigureAwait(true);
    else await session.ConnectAsync(scope, cancellationToken).ConfigureAwait(true);
    Refresh();
  }

  public async Task RefreshToolsAsync(CancellationToken cancellationToken = default)
  {
    hasError = false;
    try { await session.RefreshToolsAsync(cancellationToken).ConfigureAwait(true); }
    catch (McpUnauthorizedException)
    {
      try { await session.WhenSettledAsync().ConfigureAwait(true); }
      catch
      {
        hasError = true;
        Refresh();
        throw;
      }
      Refresh();
      return;
    }
    catch (Exception error) when (error is not OperationCanceledException)
    {
      hasError = true;
      Refresh();
      throw;
    }
    Refresh();
  }

  public async Task DisconnectAsync()
  {
    hasError = false;
    await session.DisconnectAsync().ConfigureAwait(true);
    Refresh();
  }

  public void MarkError()
  {
    hasError = true;
    Refresh();
  }

  public void Refresh()
  {
    OnPropertyChanged(nameof(HasMemberSession));
    OnPropertyChanged(nameof(IsConnected));
    OnPropertyChanged(nameof(IsBusy));
    OnPropertyChanged(nameof(RequestWriteAccess));
    OnPropertyChanged(nameof(CanConnect));
    OnPropertyChanged(nameof(CanAuthorize));
    OnPropertyChanged(nameof(CanDisconnect));
    OnPropertyChanged(nameof(HasError));
    OnPropertyChanged(nameof(ToolNames));
  }
}
