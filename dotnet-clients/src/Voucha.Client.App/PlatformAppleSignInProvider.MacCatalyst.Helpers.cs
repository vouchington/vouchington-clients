#if MACCATALYST
using AuthenticationServices;
using Microsoft.Maui.ApplicationModel;
using System.Security.Cryptography;
using System.Text;
using UIKit;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed partial class PlatformAppleSignInProvider
{
  private static UIWindow? FirstKeyWindow()
  {
    foreach (var scene in UIApplication.SharedApplication.ConnectedScenes)
    {
      if (scene is not UIWindowScene windowScene)
      {
        continue;
      }

      foreach (var candidate in windowScene.Windows)
      {
        if (candidate.IsKeyWindow)
        {
          return candidate;
        }
      }
    }

    return null;
  }

  private static string CreateNonce()
  {
#pragma warning disable CA1308
    var bytes = RandomNumberGenerator.GetBytes(32);
    return Convert.ToHexString(bytes).ToLowerInvariant();
#pragma warning restore CA1308
  }

  private static string Sha256Hex(string value)
  {
#pragma warning disable CA1308
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(bytes).ToLowerInvariant();
#pragma warning restore CA1308
  }

  private static string? FormatUserName(ASAuthorizationAppleIdCredential credential)
  {
    var name = credential.FullName;
    if (name is null)
    {
      return null;
    }

    var parts = new[] { name.GivenName, name.MiddleName, name.FamilyName }
        .Where(part => !string.IsNullOrWhiteSpace(part))
        .ToArray();
    return parts.Length == 0 ? null : string.Join(" ", parts);
  }

  private void Finish(AppleSignInCredential credential)
  {
    completion?.TrySetResult(credential);
    ClearController();
  }

  private void Finish(Exception error)
  {
    completion?.TrySetException(error);
    ClearController();
  }

  private void CancelCredential(CancellationToken cancellationToken)
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
      rawNonce = null;
    }

    if (controllerToDispose is null)
    {
      return;
    }

    MainThread.BeginInvokeOnMainThread(() =>
    {
      controllerToDispose.Delegate = null;
      controllerToDispose.PresentationContextProvider = null;
      controllerToDispose.Dispose();
    });
  }
}
#endif
