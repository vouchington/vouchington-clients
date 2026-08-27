using System.Security.Cryptography;
using System.Text;

namespace Voucha.Client.Core.Auth;

public sealed record NativeAuthorizationCompletionProof(string Verifier, string Challenge)
{
  public static NativeAuthorizationCompletionProof Create()
  {
    var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
    return FromVerifier(verifier);
  }

  public static NativeAuthorizationCompletionProof FromVerifier(string verifier) =>
      new(
          verifier,
          Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(verifier))));

  private static string Base64Url(byte[] bytes) =>
      Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
