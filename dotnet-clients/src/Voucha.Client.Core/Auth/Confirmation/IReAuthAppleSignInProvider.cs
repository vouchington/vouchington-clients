namespace Voucha.Client.Core.Auth;

public interface IReAuthAppleSignInProvider
{
  Task<AppleSignInCredential> GetCredentialAsync(string nonce, CancellationToken cancellationToken = default);
}
