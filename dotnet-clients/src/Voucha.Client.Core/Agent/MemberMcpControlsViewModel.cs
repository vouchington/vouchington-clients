using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Agent;

/// <summary>Presentation state for the reachable Windows chat MCP connection controls.</summary>
public sealed class MemberMcpControlsViewModel(MemberMcpSession session) : ObservableObject
{
  private bool hasError;

  public bool HasMemberSession => session.HasMemberSession;
  public bool IsConnected => session.Status == MemberMcpSessionStatus.Connected;
  public bool IsBusy => session.Status == MemberMcpSessionStatus.Connecting;
  public bool CanConnect => HasMemberSession && !IsConnected && !IsBusy;
  public bool HasError => hasError || session.Status == MemberMcpSessionStatus.Error;

  public async Task ConnectAsync(CancellationToken cancellationToken = default)
  {
    hasError = false;
    Refresh();
    await session.ConnectAsync(cancellationToken).ConfigureAwait(true);
    Refresh();
  }

  public async Task RefreshToolsAsync(CancellationToken cancellationToken = default)
  {
    hasError = false;
    try { await session.RefreshToolsAsync(cancellationToken).ConfigureAwait(true); }
    catch
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
    OnPropertyChanged(nameof(CanConnect));
    OnPropertyChanged(nameof(HasError));
  }
}
