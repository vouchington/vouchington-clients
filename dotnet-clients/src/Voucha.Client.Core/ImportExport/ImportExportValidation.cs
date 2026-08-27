using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ImportExport;

public static class ImportExportValidation
{
  public const int MaximumRows = 500;
  public const int MaximumBodyBytes = 2 * 1024 * 1024;

  public static IReadOnlyList<string> Lines(string input) =>
      (input ?? string.Empty).Split('\n')
          .Select(value => value.Trim())
          .Where(value => value.Length > 0)
          .ToArray();

  public static UiText? Validate(SourceImportRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);
    int rowCount;
    try
    {
      rowCount = request.Format switch
      {
        SourceImportFormat.Urls => request.Urls?.Count(value => !string.IsNullOrWhiteSpace(value)) ?? 0,
        SourceImportFormat.Csv => SourceImportParser.CsvUrls(request.Text ?? string.Empty).Count,
        SourceImportFormat.Opml => SourceImportParser.OpmlUrls(request.Text ?? string.Empty).Count,
        _ => 0,
      };
    }
    catch (InvalidDataException error)
    {
      return UiText.ExternalContent(error.Message);
    }
    if (rowCount == 0)
      return UiText.Localized(UiMessageKey.NativeSwiftImportExportAddSource);
    if (rowCount > MaximumRows)
      return UiText.Localized(
          UiMessageKey.NativeSwiftImportExportSourceLimit,
          ("count", MaximumRows));
    object body = request.Format switch
    {
      SourceImportFormat.Urls => new RssFeedUrlsImportBody(request.Urls ?? []),
      SourceImportFormat.Csv => new RssFeedCsvImportBody(request.Text ?? string.Empty),
      SourceImportFormat.Opml => new RssFeedOpmlImportBody(request.Text ?? string.Empty),
      _ => throw new ArgumentOutOfRangeException(nameof(request)),
    };
    return EncodedSize(body) > MaximumBodyBytes
        ? UiText.Localized(UiMessageKey.NativeSwiftImportExportSourceFileTooLarge)
        : null;
  }

  public static UiText? ValidateTopics(IReadOnlyList<string> names)
  {
    ArgumentNullException.ThrowIfNull(names);
    if (names.Count == 0)
      return UiText.Localized(UiMessageKey.NativeSwiftImportExportAddTopic);
    if (names.Count > MaximumRows)
      return UiText.Localized(
          UiMessageKey.NativeSwiftImportExportTopicLimit,
          ("count", MaximumRows));
    return EncodedSize(new TopicImportBody(names)) > MaximumBodyBytes
        ? UiText.Localized(UiMessageKey.NativeSwiftImportExportTopicFileTooLarge)
        : null;
  }

  private static int EncodedSize(object body) =>
      JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), VouchaApiJson.Options).Length;
}
