using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private async Task HandleAppLinkAsync(Uri url)
  {
    if (await serviceProvider.GetRequiredService<FreshAccountBroker>().HandleCallbackAsync(url).ConfigureAwait(true)) return;
    await HandleOrdinaryAppLinkAsync(url).ConfigureAwait(true);
  }
}
