namespace Voucha.Client.Core.Localization;

public static class UiCardinalRules
{
  public static string Select(string locale, decimal value)
  {
    var absoluteValue = Math.Abs(value);
    return locale switch
    {
      "fr" or "pt" when absoluteValue < 2 => "one",
      "en" or "es" when absoluteValue == 1 => "one",
      _ => "other",
    };
  }
}
