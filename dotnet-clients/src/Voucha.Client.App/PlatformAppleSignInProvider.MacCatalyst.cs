#if MACCATALYST
using AuthenticationServices;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using System.Text;
using UIKit;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed partial class PlatformAppleSignInProvider :
    NSObject,
    IAppleSignInProvider,
    IASAuthorizationControllerDelegate,
    IASAuthorizationControllerPresentationContextProviding
{
  private readonly object syncLock = new();
  private TaskCompletionSource<AppleSignInCredential>? completion;
  private CancellationTokenRegistration cancellationRegistration;
  private ASAuthorizationController? controller;
  private string? rawNonce;

  public Task<AppleSignInCredential> GetCredentialAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    lock (syncLock)
    {
      if (completion is not null)
      {
        throw new InvalidOperationException("Apple sign-in is already in progress.");
      }

      completion = new TaskCompletionSource<AppleSignInCredential>(
          TaskCreationOptions.RunContinuationsAsynchronously);
    }

    rawNonce = CreateNonce();
    ASAuthorizationAppleIdRequest request;
    using (var provider = new ASAuthorizationAppleIdProvider())
    {
      request = provider.CreateRequest();
      request.RequestedScopes = [ASAuthorizationScope.FullName, ASAuthorizationScope.Email];
      request.Nonce = Sha256Hex(rawNonce);
    }

    controller = new ASAuthorizationController([request])
    {
      Delegate = this,
      PresentationContextProvider = this,
    };

    cancellationRegistration = cancellationToken.Register(
        () => CancelCredential(cancellationToken));

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
    var window = Platform.GetCurrentUIViewController()?.View?.Window ?? FirstKeyWindow();

    return window ??
        throw new InvalidOperationException("A presentation window is required for Apple sign-in.");
  }

  [Export("authorizationController:didCompleteWithAuthorization:")]
  public void DidComplete(ASAuthorizationController controller, ASAuthorization authorization)
  {
    if (authorization.GetCredential<ASAuthorizationAppleIdCredential>() is not { } credential)
    {
      Finish(new InvalidOperationException("AuthenticationServices returned an invalid Apple credential."));
      return;
    }

    var identityToken = credential.IdentityToken?.ToArray();
    if (identityToken is null || identityToken.Length == 0)
    {
      Finish(new InvalidOperationException("Apple Sign-In did not return an identity token."));
      return;
    }

    Finish(new AppleSignInCredential(
        Encoding.UTF8.GetString(identityToken),
        rawNonce ?? throw new InvalidOperationException("Apple Sign-In nonce was lost."),
        FormatUserName(credential)));
  }

  [Export("authorizationController:didCompleteWithError:")]
  public void DidComplete(ASAuthorizationController controller, NSError error)
  {
    if (error.Code == (nint)ASAuthorizationError.Canceled)
    {
      Finish(new OperationCanceledException());
      return;
    }

    Finish(new InvalidOperationException(error.LocalizedDescription));
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

}
#endif
