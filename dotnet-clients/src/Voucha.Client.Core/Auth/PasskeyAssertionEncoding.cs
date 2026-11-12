namespace Voucha.Client.Core.Auth;

public static class PasskeyAssertionEncoding
{
  public static PasskeyAssertionResponse Create(byte[] credentialId, byte[] authenticatorData,
      byte[] clientDataJson, byte[] signature, byte[]? userHandle)
  {
    ArgumentNullException.ThrowIfNull(credentialId);
    ArgumentNullException.ThrowIfNull(authenticatorData);
    ArgumentNullException.ThrowIfNull(clientDataJson);
    ArgumentNullException.ThrowIfNull(signature);
    var id = Encode(credentialId);
    return new(id, id, new(Encode(authenticatorData), Encode(clientDataJson), Encode(signature),
        userHandle is null ? null : Encode(userHandle)));
  }

  public static byte[] Decode(string value)
  {
    ArgumentNullException.ThrowIfNull(value);
    var base64 = value.Replace('-', '+').Replace('_', '/');
    return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
  }

  private static string Encode(byte[] value)
  {
    ArgumentNullException.ThrowIfNull(value);
    return Convert.ToBase64String(value).Replace('+', '-').Replace('/', '_').TrimEnd('=');
  }
}

public enum PasskeyAuthenticatorKind { Platform, SecurityKey }

public sealed record PasskeyAssertionRequestPlan(PasskeyAuthenticatorKind Kind, string RpId,
    byte[] Challenge, string? UserVerification, IReadOnlyList<byte[]> AllowedCredentialIds)
{
  public static IReadOnlyList<PasskeyAssertionRequestPlan> Create(PasskeyAuthenticationOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);
    var challenge = PasskeyAssertionEncoding.Decode(options.Challenge);
    var ids = (options.AllowCredentials ?? []).Select(c => PasskeyAssertionEncoding.Decode(c.Id)).ToArray();
    return [new(PasskeyAuthenticatorKind.Platform, options.RpId, challenge, options.UserVerification, ids),
        new(PasskeyAuthenticatorKind.SecurityKey, options.RpId, challenge, options.UserVerification, ids)];
  }
}
