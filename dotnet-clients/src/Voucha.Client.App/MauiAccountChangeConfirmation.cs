using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public sealed class MauiAccountChangeConfirmation(IServiceProvider services) : IAccountChangeConfirmation
{
  private readonly SemaphoreSlim gate = new(1, 1);

  public async Task<AccountChangeProof> ConfirmAsync(CancellationToken cancellationToken = default)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    FreshAccountConfirmationViewModel? model = null;
    NavigationPage? modal = null;
    INavigation? navigation = null;
    try
    {
      return await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        var session = services.GetRequiredService<ISessionStore>();
        model = new FreshAccountConfirmationViewModel(services.GetRequiredService<IFreshAccountProofService>(),
            CaptureBinding, session, services.GetRequiredService<IUiLocalization>(),
            services.GetRequiredService<IUiLocaleController>(), cancellationToken);
        modal = new NavigationPage(new FreshAccountConfirmationPage(model));
        navigation = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
            ?? throw new OperationCanceledException();
        await navigation.PushModalAsync(modal).ConfigureAwait(true);
        _ = model.InitializeAsync();
        return await model.Completion.WaitAsync(cancellationToken).ConfigureAwait(true);
      }).ConfigureAwait(false);
    }
    finally
    {
      try
      {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
          model?.Cancel();
          if (modal is not null && navigation?.ModalStack.LastOrDefault() == modal)
            await navigation.PopModalAsync().ConfigureAwait(true);
        }).ConfigureAwait(false);
      }
      finally
      {
        try { model?.Dispose(); }
        finally { gate.Release(); }
      }
    }
  }

  private string? CaptureBinding() => AccountChangeSessionBinding.Capture(
      services.GetRequiredService<ISessionStore>(), services.GetRequiredService<SessionCookieJar>(),
      services.GetRequiredService<AppConfig>().ApiBaseUrl);
}
