namespace Voucha.Client.Core.Auth;

public static class EmailVerificationCode
{
  public static string Normalize(string value)
  {
    ArgumentNullException.ThrowIfNull(value);
    return string.Concat(value.Select(character =>
        character is >= 'a' and <= 'z' ? (char)(character - ('a' - 'A')) : character));
  }

  public static bool IsValid(string? value) => value is { Length: 10 } &&
      Normalize(value).All(character => character is >= 'A' and <= 'Z' or >= '2' and <= '7');

  public static string RequireValid(string value)
  {
    ArgumentNullException.ThrowIfNull(value);
    if (!IsValid(value)) throw new ArgumentException(null, nameof(value));
    return Normalize(value);
  }
}
