using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ImportExport;

public sealed partial class ImportExportViewModel
{
  private string? sourcePageCursor;
  private readonly HashSet<string> sourcePageCursors = new(StringComparer.Ordinal);
  private readonly List<ImportResult> sourcePageRows = [];
  private readonly Dictionary<string, int> sourcePagePositions = new(StringComparer.Ordinal);
  private RssFeedImportSummary? sourcePageSummary;
  private bool sourcePagesStartedTerminal;
  private bool sourceSnapshotStartedTerminal;

  private void ResetSourcePages()
  {
    sourcePageCursor = null;
    sourcePageCursors.Clear();
    sourcePageRows.Clear();
    sourcePagePositions.Clear();
    sourcePageSummary = null;
  }

  // One bounded server page per tick; failures preserve the cursor and accumulated rows.
  private async Task<RssFeedImportStatus?> LoadSourcePagesAsync(int operation, string batch, CancellationToken token)
  {
    var page = await service.GetSourceStatusAsync(batch, sourcePageCursor, token).ConfigureAwait(true);
    token.ThrowIfCancellationRequested();
    if (operation != generation || BatchId != batch) throw new OperationCanceledException(token);
    if (page.Import.Id != batch)
    {
      if (sourcePageCursor is null) return null;
      throw new SourceStatusPageException();
    }
    if (sourcePageSummary is { } previous && (page.Import.TotalRows != previous.TotalRows ||
        page.Import.CompletedRows + page.Import.FailedRows < previous.CompletedRows + previous.FailedRows ||
        page.Import.PendingRows > previous.PendingRows))
      throw new SourceStatusPageException();
    var next = page.PageInfo?.HasNextPage == true ? page.PageInfo.EndCursor : null;
    if (page.PageInfo?.HasNextPage == true &&
        (page.Rows.Count == 0 || string.IsNullOrEmpty(next) || sourcePageCursors.Contains(next)))
      throw new SourceStatusPageException();
    if (sourcePageSummary is null) sourcePagesStartedTerminal = page.Import.IsTerminal();
    foreach (var row in page.Rows)
    {
      if (row.Id is { } id && sourcePagePositions.TryGetValue(id, out var position)) sourcePageRows[position] = row;
      else
      {
        if (row.Id is { } newId) sourcePagePositions[newId] = sourcePageRows.Count;
        sourcePageRows.Add(row);
      }
    }
    sourcePageSummary = page.Import;
    sourcePageCursor = next;
    if (next is not null)
    {
      sourcePageCursors.Add(next);
      return null;
    }
    var snapshot = page with { Rows = sourcePageRows.ToArray() };
    sourceSnapshotStartedTerminal = sourcePagesStartedTerminal;
    ResetSourcePages();
    return snapshot;
  }
}
