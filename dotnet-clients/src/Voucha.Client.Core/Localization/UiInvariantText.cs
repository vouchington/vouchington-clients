namespace Voucha.Client.Core.Localization;

public readonly record struct UiInvariantText(string Value)
{
  public static UiInvariantText RequiredMarker { get; } = new("*");

  public static UiInvariantText ZeroDuration { get; } = new("0:00");

  public static UiInvariantText UnknownDuration { get; } = new("--:--");

  public static UiInvariantText VouchaBrand { get; } = new("Voucha");

  public string AppendTo(string value) =>
      string.IsNullOrEmpty(value) ? Value : $"{value} {Value}";
}

public readonly record struct UiUserHandle(string Value)
{
  public static UiUserHandle FromUsername(string? username)
  {
    var normalized = string.IsNullOrWhiteSpace(username) ? string.Empty : username.Trim();
    return new(normalized.Length == 0 ? string.Empty : $"@{normalized}");
  }
}

public readonly record struct UiExternalProviderText(string Value)
{
  public static UiExternalProviderText AmazonSes { get; } = new("SES");

  public static UiExternalProviderText GmailSmtp { get; } = new("Gmail SMTP");
}

public readonly record struct UiUserInputText(string Value)
{
  public static UiUserInputText FromValue(object? value) =>
      new(value?.ToString() ?? string.Empty);
}

public readonly record struct UiExternalContentText(string Value)
{
  public static UiExternalContentText FromUri(Uri? value) =>
      new(value?.ToString() ?? string.Empty);
}
