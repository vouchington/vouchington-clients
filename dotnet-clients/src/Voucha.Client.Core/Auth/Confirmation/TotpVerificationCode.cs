namespace Voucha.Client.Core.Auth;

public static class TotpVerificationCode
{
  public static bool IsValid(string? code) => code is { Length: 6 } &&
      code.All(character => character is >= '0' and <= '9');

  public static string RequireValid(string code) =>
      IsValid(code) ? code : throw new ArgumentException(null, nameof(code));
}
