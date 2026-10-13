#if WINDOWS
using Voucha.Client.Core.Agent;

namespace Voucha.Client.App;

/// <summary>Windows MCP OAuth uses the external browser and a one-shot loopback callback.</summary>
public sealed class MauiMemberMcpSystemBrowser : IMemberMcpSystemBrowser
{
  public async Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var opened = await Browser.Default.OpenAsync(authorizationUri, BrowserLaunchMode.External)
        .ConfigureAwait(false);
    cancellationToken.ThrowIfCancellationRequested();
    if (!opened) throw new InvalidOperationException("The system browser could not be opened.");
  }
}
#endif
