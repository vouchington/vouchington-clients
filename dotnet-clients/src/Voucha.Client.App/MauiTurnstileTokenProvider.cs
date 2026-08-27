using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;

namespace Voucha.Client.App;

public interface ITurnstileTokenProvider
{
  Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class MauiTurnstileTokenProvider : ITurnstileTokenProvider
{
  private readonly AppConfig appConfig;

  public MauiTurnstileTokenProvider(AppConfig appConfig) =>
      this.appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));

  public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var page = new TurnstileChallengePage(appConfig);
    var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

    void HandleTokenCaptured(object? sender, TurnstileTokenCapturedEventArgs args)
    {
      if (!string.IsNullOrWhiteSpace(args.Token))
      {
        completion.TrySetResult(args.Token);
      }
    }

    void HandleCloseRequested(object? sender, EventArgs e) =>
        completion.TrySetCanceled(cancellationToken);

    page.TokenCaptured += HandleTokenCaptured;
    page.CloseRequested += HandleCloseRequested;

    using var cancellationRegistration = cancellationToken.Register(
        () => completion.TrySetCanceled(cancellationToken));
    var navigation = GetNavigation();

    try
    {
      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        await navigation.PushModalAsync(page);
      }).ConfigureAwait(true);

      return await completion.Task.ConfigureAwait(true);
    }
    finally
    {
      page.TokenCaptured -= HandleTokenCaptured;
      page.CloseRequested -= HandleCloseRequested;

      await MainThread.InvokeOnMainThreadAsync(async () =>
      {
        if (IsModalPageOpen(navigation, page))
        {
          await navigation.PopModalAsync();
        }
      }).ConfigureAwait(true);
    }
  }

  private static INavigation GetNavigation() =>
      Shell.Current?.Navigation
          ?? FirstWindowNavigation()
          ?? throw new InvalidOperationException("A navigation stack is required for Turnstile verification.");

  private static INavigation? FirstWindowNavigation()
  {
    var windows = Application.Current?.Windows;
    return windows is { Count: > 0 } ? windows[0].Page?.Navigation : null;
  }

  private static bool IsModalPageOpen(INavigation navigation, Page page)
  {
    var modalStack = navigation.ModalStack;
    for (var index = 0; index < modalStack.Count; index++)
    {
      if (ReferenceEquals(modalStack[index], page))
      {
        return true;
      }
    }

    return false;
  }
}
