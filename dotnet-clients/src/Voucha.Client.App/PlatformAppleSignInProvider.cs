#if !MACCATALYST
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed class PlatformAppleSignInProvider : IAppleSignInProvider, IReAuthAppleSignInProvider
{
  private readonly AppleSignInCallbackStore callbackStore;

  public PlatformAppleSignInProvider(AppleSignInCallbackStore callbackStore) =>
      this.callbackStore = callbackStore ?? throw new ArgumentNullException(nameof(callbackStore));

  public Task<AppleSignInCredential> GetCredentialAsync(string nonce, CancellationToken cancellationToken = default) =>
      callbackStore.StartAsync(cancellationToken, nonce);

  public Task<AppleSignInCredential> GetCredentialAsync(CancellationToken cancellationToken = default)
  {
    return callbackStore.StartAsync(cancellationToken);
  }
}
#endif
