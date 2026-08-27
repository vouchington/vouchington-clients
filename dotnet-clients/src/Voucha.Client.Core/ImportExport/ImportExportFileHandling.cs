using System.Text;

namespace Voucha.Client.Core.ImportExport;

public sealed record PickedImportFile(
    string FileName,
    Func<CancellationToken, Task<Stream>> OpenReadAsync);

public sealed record ImportedSourceFile(SourceImportFormat Format, string Text);

public interface IImportExportFilePicker
{
  Task<PickedImportFile?> PickAsync(CancellationToken token);
}

public interface IImportExportFileReader
{
  Task<string> ReadUtf8Async(PickedImportFile file, CancellationToken token);
}

public interface IImportExportDocumentSharer
{
  Task ShareAsync(ExportDocument document, CancellationToken token);
}

public interface IImportExportFileAdapter
{
  Task<ImportedSourceFile?> PickSourceFileAsync(CancellationToken token);
  Task ShareAsync(ExportDocument document, CancellationToken token);
}

public sealed class ImportExportFileAdapter(
    IImportExportFilePicker picker,
    IImportExportFileReader reader,
    IImportExportDocumentSharer sharer) : IImportExportFileAdapter
{
  public async Task<ImportedSourceFile?> PickSourceFileAsync(CancellationToken token)
  {
    var file = await picker.PickAsync(token).ConfigureAwait(false);
    if (file is null) return null;
    var format = Path.GetExtension(file.FileName).ToUpperInvariant() switch
    {
      ".CSV" => SourceImportFormat.Csv,
      ".OPML" or ".XML" => SourceImportFormat.Opml,
      _ => throw new InvalidDataException("Choose a CSV or OPML file."),
    };
    var text = await reader.ReadUtf8Async(file, token).ConfigureAwait(false);
    return new(format, text);
  }

  public Task ShareAsync(ExportDocument document, CancellationToken token) =>
      sharer.ShareAsync(document, token);
}

public sealed class StrictUtf8ImportExportFileReader : IImportExportFileReader
{
  private static readonly UTF8Encoding StrictUtf8 = new(false, true);

  public async Task<string> ReadUtf8Async(PickedImportFile file, CancellationToken token)
  {
    ArgumentNullException.ThrowIfNull(file);
    var stream = await file.OpenReadAsync(token).ConfigureAwait(false);
    await using (stream.ConfigureAwait(false))
    {
      if (stream.CanSeek && stream.Length - stream.Position > ImportExportValidation.MaximumBodyBytes)
      {
        throw FileTooLarge();
      }

      var bytes = new byte[ImportExportValidation.MaximumBodyBytes + 1];
      var read = await stream.ReadAtLeastAsync(
          bytes.AsMemory(), bytes.Length, throwOnEndOfStream: false, cancellationToken: token).ConfigureAwait(false);
      if (read > ImportExportValidation.MaximumBodyBytes) throw FileTooLarge();
      var offset = read >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
      return StrictUtf8.GetString(bytes.AsSpan(offset, read - offset));
    }
  }

  private static InvalidDataException FileTooLarge() =>
      new("Import files must be 2 MiB or smaller.");
}
