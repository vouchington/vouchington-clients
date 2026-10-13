using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Agent;

namespace Voucha.Client.App.Pages;

public partial class ChatMemberMcpControlsView : ContentView
{
  private readonly MemberMcpSession session;
  private readonly MemberMcpControlsViewModel viewModel;
  private bool attached;

  public ChatMemberMcpControlsView(MemberMcpSession session)
  {
    this.session = session;
    viewModel = new MemberMcpControlsViewModel(session);
    InitializeComponent();
    BindingContext = viewModel;
    Loaded += OnLoaded;
    Unloaded += OnUnloaded;
  }

  private void OnLoaded(object? sender, EventArgs args)
  {
    if (attached) return;
    attached = true;
    session.Changed += OnSessionChanged;
    viewModel.Refresh();
  }

  private void OnUnloaded(object? sender, EventArgs args)
  {
    if (!attached) return;
    attached = false;
    session.Changed -= OnSessionChanged;
  }

  private void OnSessionChanged(object? sender, EventArgs args) =>
      Dispatcher.Dispatch(viewModel.Refresh);

  [SuppressMessage("Design", "CA1031", Justification = "MAUI click handlers show a localized error state instead of throwing.")]
  private async void OnConnectClicked(object? sender, EventArgs args)
  {
    try { await viewModel.ConnectAsync().ConfigureAwait(true); }
    catch (OperationCanceledException) { }
    catch (Exception) { viewModel.MarkError(); }
  }

  [SuppressMessage("Design", "CA1031", Justification = "MAUI click handlers show a localized error state instead of throwing.")]
  private async void OnRefreshClicked(object? sender, EventArgs args)
  {
    try { await viewModel.RefreshToolsAsync().ConfigureAwait(true); }
    catch (OperationCanceledException) { }
    catch (Exception) { viewModel.MarkError(); }
  }

  [SuppressMessage("Design", "CA1031", Justification = "MAUI click handlers show a localized error state instead of throwing.")]
  private async void OnDisconnectClicked(object? sender, EventArgs args)
  {
    try { await viewModel.DisconnectAsync().ConfigureAwait(true); }
    catch (OperationCanceledException) { }
    catch (Exception) { viewModel.MarkError(); }
  }
}
