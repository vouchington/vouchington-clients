using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.SourceCreation;

public sealed partial class SourceCreationViewModel
{
  public Task CreateAsync() => RunAsync(create: true);
  public Task RetryAsync() => RunAsync(create: false);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Protocol and transport errors become localized retry state.")]
  private async Task RunAsync(bool create)
  {
    if (create ? !CanCreate : !CanRetry) return;
    if (!create && service.ApiOrigin != origin)
    {
      ResetIdentity();
      return;
    }
    error = null;
    if (create && (!Uri.TryCreate(Input.Trim(), UriKind.Absolute, out var parsed) ||
        parsed.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(parsed.UserInfo)))
    {
      Fail();
      return;
    }
    var operation = ++generation;
    cancellation?.Dispose();
    cancellation = new();
    var token = cancellation.Token;
    var account = session.Current.Identity?.Id;
    origin = service.ApiOrigin;
    busy = true;
    Publish();
    bool Current() => !token.IsCancellationRequested && operation == generation &&
        account == session.Current.Identity?.Id && origin == service.ApiOrigin;
    try
    {
      if (create)
      {
        submitted = true;
        var submission = await service.CreateAsync(new Uri(Input.Trim()), token).ConfigureAwait(true);
        if (!Current()) return;
        // Retain the accepted identity before validating so protocol errors can never repeat the POST.
        importId = submission.Import.Id;
        summary = submission.Import;
        var expected = $"/api/v1/my/import/rss-feeds/{Uri.EscapeDataString(importId)}";
        var status = new Uri(origin, submission.StatusUrl);
        if (string.IsNullOrWhiteSpace(importId) || status.GetLeftPart(UriPartial.Authority) != origin.GetLeftPart(UriPartial.Authority) ||
            status.PathAndQuery != expected || status.Fragment.Length != 0 || status.UserInfo.Length != 0)
          throw new InvalidOperationException();
        validated = true;
      }
      for (var tick = 0; tick < 30; tick++)
      {
        var status = await service.ReadAsync(importId!, token).ConfigureAwait(true);
        if (!Current()) return;
        if (status.Import.Id != importId || status.Import.TotalRows != 1 || status.Rows.Count != 1 ||
            status.PageInfo?.HasNextPage == true) throw new InvalidOperationException();
        if (summary is { } previous && (status.Import.CompletedRows + status.Import.FailedRows <
            previous.CompletedRows + previous.FailedRows || status.Import.PendingRows > previous.PendingRows))
          throw new InvalidOperationException();
        if (status.Import.IsTerminal() && status.Rows[0].Status != Api.ImportResultStatus.Pending && (status.Import.PendingRows != 0 ||
            (status.Rows[0].IsTerminalFailure() ? status.Import.FailedRows != 1 :
              status.Import.FailedRows != 0 || status.Import.CompletedRows != 1)))
          throw new InvalidOperationException();
        summary = status.Import;
        result = status.Rows[0];
        Publish();
        if (summary.IsTerminal() && result.Status != Api.ImportResultStatus.Pending)
        {
          if (!result.IsTerminalFailure() && result.EntityId is { Length: > 0 } feedId && string.IsNullOrWhiteSpace(result.TopicSlug))
          {
            var detail = await service.ResolveAsync(feedId, token).ConfigureAwait(true);
            if (!Current()) return;
            if (detail.Id != feedId || string.IsNullOrWhiteSpace(detail.TopicId)) throw new InvalidOperationException();
            resolvedTopic = detail.TopicId;
          }
          if (!result.IsTerminalFailure() && !CanOpen) throw new InvalidOperationException();
          return;
        }
        if (tick < 29) await delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(true);
        if (!Current()) return;
      }
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception) { if (Current()) Fail(); }
    finally { if (operation == generation) { busy = false; Publish(); } }
  }

  private void Fail()
  {
    error = UiText.Localized(UiMessageKey.NativeDotnetCsharpActionFailed);
    Publish();
  }
}
