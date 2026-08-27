#if MACCATALYST
using AuthenticationServices;
using Foundation;
using UIKit;
#endif

using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

#if MACCATALYST
public sealed class PlatformPasskeyAssertionProvider :
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

    ASAuthorizationPlatformPublicKeyCredentialAssertionRequest request;
    using (var provider = new ASAuthorizationPlatformPublicKeyCredentialProvider(options.RpId))
    {
      request = provider.CreateCredentialAssertionRequest(NSData.FromArray(Base64UrlDecode(options.Challenge)));
      request.UserVerificationPreference = UserVerificationPreference(options.UserVerification);
      request.AllowedCredentials = (options.AllowCredentials ?? [])
          .Select(credential => new ASAuthorizationPlatformPublicKeyCredentialDescriptor(
              NSData.FromArray(Base64UrlDecode(credential.Id))))
          .ToArray();
    }

    controller = new ASAuthorizationController([request])
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
    if (authorization.GetCredential<ASAuthorizationPlatformPublicKeyCredentialAssertion>() is not { } assertion)
    {
      Finish(new InvalidOperationException("AuthenticationServices returned an invalid passkey assertion."));
      return;
    }

    var credentialId = assertion.CredentialId.ToArray().Base64UrlEncode();
    Finish(new PasskeyAssertionResponse(
        credentialId,
        credentialId,
        new PasskeyAssertionAuthenticatorResponse(
            assertion.RawAuthenticatorData.ToArray().Base64UrlEncode(),
            assertion.RawClientDataJson.ToArray().Base64UrlEncode(),
            assertion.Signature.ToArray().Base64UrlEncode(),
            assertion.UserId.ToArray().Base64UrlEncode())));
  }

  [Export("authorizationController:didCompleteWithError:")]
  public void DidComplete(ASAuthorizationController controller, NSError error) =>
      Finish(new InvalidOperationException(error.LocalizedDescription));

  private static NSString UserVerificationPreference(string? preference) =>
      preference switch
      {
        "required" => ASAuthorizationPublicKeyCredentialUserVerificationPreference.Required,
        "discouraged" => ASAuthorizationPublicKeyCredentialUserVerificationPreference.Discouraged,
        _ => ASAuthorizationPublicKeyCredentialUserVerificationPreference.Preferred,
      };

  private static byte[] Base64UrlDecode(string value)
  {
    var base64 = value.Replace('-', '+').Replace('_', '/');
    var padding = base64.Length % 4;
    if (padding > 0)
    {
      base64 = base64.PadRight(base64.Length + 4 - padding, '=');
    }

    return Convert.FromBase64String(base64);
  }

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
