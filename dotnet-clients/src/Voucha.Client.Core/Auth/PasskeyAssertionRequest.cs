using System.Text.Json;

namespace Voucha.Client.Core.Auth;

/// <summary>Validates server options against the trusted app origin before native authentication.</summary>
public sealed record PasskeyAssertionRequest(
    string RpId, ReadOnlyMemory<byte> ClientDataJson, IReadOnlyList<ReadOnlyMemory<byte>> CredentialIds, string UserVerification)
{
  public static PasskeyAssertionRequest Create(PasskeyAuthenticationOptions options, Uri origin)
  {
    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(origin);
    // An untrusted server response must never choose the native caller's origin or another RP.
    if (!origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps ||
        !string.IsNullOrEmpty(origin.UserInfo) ||
        !string.Equals(options.RpId, origin.IdnHost, StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException("Passkey relying party must match the configured HTTPS web origin.");
    }

    var challenge = Decode(options.Challenge);
    if (challenge.Length == 0)
    {
      throw new InvalidOperationException("A passkey challenge is required.");
    }

    var credentials = (options.AllowCredentials ?? []).Select(credential => (ReadOnlyMemory<byte>)Decode(credential.Id)).ToArray();
    if (credentials.Any(credential => credential.Length == 0))
    {
      throw new InvalidOperationException("Passkey credential identifiers must not be empty.");
    }

    var clientData = JsonSerializer.SerializeToUtf8Bytes(new
    {
      type = "webauthn.get",
      challenge = Encode(challenge),
      origin = origin.GetLeftPart(UriPartial.Authority),
      crossOrigin = false,
    });
    var userVerification = options.UserVerification ?? "preferred";
    if (userVerification is not "required" and not "preferred" and not "discouraged")
    {
      throw new InvalidOperationException("Invalid passkey user verification requirement.");
    }

    return new PasskeyAssertionRequest(options.RpId, clientData, credentials, userVerification);
  }

  public PasskeyAssertionResponse Complete(byte[] credentialId, byte[] authenticatorData, byte[] signature, byte[] userId)
  {
    ArgumentNullException.ThrowIfNull(credentialId);
    ArgumentNullException.ThrowIfNull(authenticatorData);
    ArgumentNullException.ThrowIfNull(signature);
    ArgumentNullException.ThrowIfNull(userId);
    if (credentialId.Length == 0 || authenticatorData.Length == 0 || signature.Length == 0 ||
        (CredentialIds.Count > 0 && !CredentialIds.Any(id => id.Span.SequenceEqual(credentialId))))
    {
      throw new InvalidOperationException("The authenticator returned an invalid passkey assertion.");
    }

    var id = Encode(credentialId);
    return new PasskeyAssertionResponse(id, id, new PasskeyAssertionAuthenticatorResponse(
        Encode(authenticatorData), Encode(ClientDataJson.Span),
        Encode(signature), Encode(userId)));
  }

  private static string Encode(ReadOnlySpan<byte> bytes) =>
      Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

  private static byte[] Decode(string value)
  {
    ArgumentNullException.ThrowIfNull(value);
    if (value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
    {
      throw new FormatException("Invalid base64url passkey data.");
    }

    var base64 = value.Replace('-', '+').Replace('_', '/');
    return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
  }
}
