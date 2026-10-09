namespace Voucha.Client.Core.Localization;

public static class NativeLocalizationSelectors
{
  public const string Consumer = "dotnet";

  public static readonly IReadOnlyList<string> Chrome =
  [
      "common.*",
      "extracted.*",
      "images.uploadPreviewUnavailable",
      "nav.*",
      "settings.*",
      "shared.*",
      "native.common.*",
      "native.copyrightNotices.*",
      "native.auth.*",
      "native.apiKeys.*",
      "native.credentials.*",
      "native.dotnet.*",
      "native.language.*",
      "native.legal.*",
      "native.moderation.*",
      "native.swift.*",
      "native.taxonomy.*",
  ];

  public static string ChromeJoined { get; } = string.Join(',', Chrome);
}
