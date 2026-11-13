using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed class PlatformReAuthProviderAssertion(IAppleSignInProvider apple) : IReAuthProviderAssertion
{
  public async Task<string> GetCredentialAsync(string provider, string nonce, CancellationToken cancellationToken)
  {
    if (provider != "apple" || apple is not IReAuthAppleSignInProvider fresh) throw new NotSupportedException();
    return (await fresh.GetCredentialAsync(nonce, cancellationToken).ConfigureAwait(false)).Token;
  }
}
