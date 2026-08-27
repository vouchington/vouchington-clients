namespace Voucha.Client.App;

internal static class PasskeyByteArrayExtensions
{
  public static string Base64UrlEncode(this byte[] value) =>
      Convert.ToBase64String(value).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
