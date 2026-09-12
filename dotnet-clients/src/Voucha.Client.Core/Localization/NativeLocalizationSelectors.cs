namespace Voucha.Client.Core.Localization;

public static class NativeLocalizationSelectors
{
  public const string Consumer = "dotnet";

  public static readonly IReadOnlyList<string> Chrome =
  [
      "common.*",
      "nav.*",
      "settings.*",
      "shared.*",
      "native.common.*",
      "native.auth.*",
      "native.dotnet.*",
  ];

  public static string ChromeJoined { get; } = string.Join(',', Chrome);
}
