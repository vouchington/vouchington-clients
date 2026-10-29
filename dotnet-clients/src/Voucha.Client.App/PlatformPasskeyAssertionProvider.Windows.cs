#if WINDOWS
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed class PlatformPasskeyAssertionProvider(AppConfig config) : IPasskeyAssertionProvider
{
  private int inProgress;

  public async Task<PasskeyAssertionResponse> GetAssertionAsync(
      PasskeyAuthenticationOptions options, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var request = PasskeyAssertionRequest.Create(options, config.WebBaseUrl ?? config.ApiBaseUrl);
    if (Interlocked.CompareExchange(ref inProgress, 1, 0) != 0)
    {
      throw new InvalidOperationException("A passkey assertion is already in progress.");
    }

    try
    {
      var windowHandle = await MainThread.InvokeOnMainThreadAsync(() =>
      {
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            as Microsoft.UI.Xaml.Window;
        return window is null ? throw new InvalidOperationException("A passkey presentation window is required.")
            : WinRT.Interop.WindowNative.GetWindowHandle(window);
      });
      // The Win32 call blocks while Windows owns the modal Hello/security-key UI.
      return await Task.Run(() => WindowsPasskeyAssertion.Get(request, windowHandle, cancellationToken),
          cancellationToken);
    }
    finally
    {
      Volatile.Write(ref inProgress, 0);
    }
  }
}
#endif
