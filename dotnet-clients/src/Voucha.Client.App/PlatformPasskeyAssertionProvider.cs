#if MACCATALYST
using AuthenticationServices;
using Foundation;
using UIKit;
#endif

using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

#if MACCATALYST
public sealed partial class PlatformPasskeyAssertionProvider :
    NSObject,
    IPasskeyAssertionProvider,
    IASAuthorizationControllerDelegate,
    IASAuthorizationControllerPresentationContextProviding
{
  private readonly object syncLock = new();
  private TaskCompletionSource<PasskeyAssertionResponse>? completion;
  private CancellationTokenRegistration cancellationRegistration;
  private ASAuthorizationController? controller;

  public Task<PasskeyAssertionResponse> GetAssertionAsync(
      PasskeyAuthenticationOptions options,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(options);

    lock (syncLock)
    {
      if (completion is not null)
      {
        throw new InvalidOperationException("A passkey assertion is already in progress.");
      }
    }

    cancellationToken.ThrowIfCancellationRequested();

    controller = new ASAuthorizationController(CreateRequests(options))
    {
      Delegate = this,
      PresentationContextProvider = this,
    };

    completion = new TaskCompletionSource<PasskeyAssertionResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    cancellationRegistration = cancellationToken.Register(() =>
        CancelAssertion(cancellationToken));

    if (cancellationToken.IsCancellationRequested)
    {
      ClearController();
      throw new OperationCanceledException(cancellationToken);
    }

    (controller ?? throw new OperationCanceledException(cancellationToken)).PerformRequests();

    return completion.Task;
  }

  public UIWindow GetPresentationAnchor(ASAuthorizationController controller)
  {
    var window = Microsoft.Maui.ApplicationModel.Platform.GetCurrentUIViewController()?.View?.Window
        ?? UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow);

    return window ??
        throw new InvalidOperationException("A presentation window is required for passkey sign-in.");
  }

  [Export("authorizationController:didCompleteWithAuthorization:")]
  public void DidComplete(ASAuthorizationController controller, ASAuthorization authorization)
  {
    using var credentialKey = new NSString("credential");
    var credential = authorization.ValueForKey(credentialKey);
    if (credential is ASAuthorizationPlatformPublicKeyCredentialAssertion platform)
    {
      Finish(PasskeyAssertionEncoding.Create(platform.CredentialId.ToArray(),
          platform.RawAuthenticatorData.ToArray(), platform.RawClientDataJson.ToArray(),
          platform.Signature.ToArray(), platform.UserId?.ToArray()));
    }
    else if (credential is ASAuthorizationSecurityKeyPublicKeyCredentialAssertion securityKey)
    {
      Finish(PasskeyAssertionEncoding.Create(securityKey.CredentialId.ToArray(),
          securityKey.RawAuthenticatorData.ToArray(), securityKey.RawClientDataJson.ToArray(),
          securityKey.Signature.ToArray(), securityKey.UserId?.ToArray()));
    }
    else
    {
      Finish(new InvalidOperationException("AuthenticationServices returned an invalid passkey assertion."));
    }
  }

  [Export("authorizationController:didCompleteWithError:")]
  public void DidComplete(ASAuthorizationController controller, NSError error) =>
      Finish(new InvalidOperationException(error.LocalizedDescription));

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      lock (syncLock)
      {
        controller?.Dispose();
        controller = null;
        cancellationRegistration.Dispose();
        completion = null;
      }
    }

    base.Dispose(disposing);
  }

  private void Finish(PasskeyAssertionResponse response)
  {
    completion?.TrySetResult(response);
    ClearController();
  }

  private void Finish(Exception error)
  {
    completion?.TrySetException(error);
    ClearController();
  }

  private void CancelAssertion(CancellationToken cancellationToken)
  {
    completion?.TrySetCanceled(cancellationToken);
    MainThread.BeginInvokeOnMainThread(ClearController);
  }

  private void ClearController()
  {
    ASAuthorizationController? controllerToDispose = null;
    lock (syncLock)
    {
      if (controller is not null)
      {
        controllerToDispose = controller;
        controller = null;
      }

      cancellationRegistration.Dispose();
      completion = null;
    }

    if (controllerToDispose is null) return;
    MainThread.BeginInvokeOnMainThread(() =>
    {
      controllerToDispose.Delegate = null;
      controllerToDispose.PresentationContextProvider = null;
      controllerToDispose.Dispose();
    });
  }
}
#else
public sealed class PlatformPasskeyAssertionProvider : IPasskeyAssertionProvider
{
  public Task<PasskeyAssertionResponse> GetAssertionAsync(
      PasskeyAuthenticationOptions options,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(options);
    cancellationToken.ThrowIfCancellationRequested();

    throw new PlatformNotSupportedException(
        "Native passkey assertion is not available in this MAUI build yet.");
  }
}
#endif
