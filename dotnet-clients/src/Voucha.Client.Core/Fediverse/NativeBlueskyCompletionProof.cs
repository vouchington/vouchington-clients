using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Voucha.Client.Core.Fediverse;

public sealed record NativeBlueskyCompletionProof(string Verifier, string Challenge)
{
  public static NativeBlueskyCompletionProof Create()
  {
    var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
    return new(verifier, Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(verifier))));
  }

  public static NativeBlueskyCompletionProof FromVerifier(string verifier) =>
      new(verifier, Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(verifier))));
}
