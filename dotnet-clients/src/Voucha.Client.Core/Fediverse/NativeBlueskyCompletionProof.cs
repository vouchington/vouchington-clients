using System.Security.Cryptography;
using System.Text;

namespace Voucha.Client.Core.Fediverse;

public sealed record NativeBlueskyCompletionProof(string Verifier, string Challenge)
{
  public static NativeBlueskyCompletionProof Create()
  {
    var verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
    return new(verifier, Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(verifier))));
  }

  public static NativeBlueskyCompletionProof FromVerifier(string verifier) =>
      new(verifier, Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(verifier))));

  private static string Base64Url(byte[] bytes) =>
      Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
