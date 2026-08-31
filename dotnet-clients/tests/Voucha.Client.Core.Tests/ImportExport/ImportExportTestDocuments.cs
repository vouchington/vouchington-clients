using Voucha.Client.Core.ImportExport;

namespace Voucha.Client.Core.Tests.ImportExport;

internal static class ImportExportTestDocuments
{
  public static ExportDocument Create(
      string fileName = "topics.json",
      string mediaType = "application/json",
      string content = "[]")
  {
    var directory = Path.Combine(Path.GetTempPath(), "voucha-import-export-tests");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, $"{Guid.NewGuid():N}-{fileName}");
    File.WriteAllText(path, content);
    return new(fileName, mediaType, path);
  }
}
