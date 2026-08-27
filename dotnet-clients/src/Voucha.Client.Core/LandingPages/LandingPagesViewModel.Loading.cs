using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading) return;
    BeginLoad();
    try
    {
      var pagesTask = service.FetchPagesAsync(cancellationToken);
      var candidatesTask = service.FetchCandidatesAsync(cancellationToken);
      await Task.WhenAll(pagesTask, candidatesTask).ConfigureAwait(true);
      var stagedPages = (await pagesTask.ConfigureAwait(true)).Results;
      var stagedCandidates = (await candidatesTask.ConfigureAwait(true)).Candidates;
      var selectedId = SelectStagedPageId(stagedPages);
      LandingPage? stagedPage = null;
      if (selectedId is not null)
      {
        stagedPage = (await service.FetchDetailAsync(selectedId, cancellationToken).ConfigureAwait(true)).LandingPage;
      }
      CommitLoad(stagedPages, stagedCandidates, stagedPage);
      CompleteLoad();
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
    }
  }

  public async Task SelectPageAsync(string pageId, CancellationToken cancellationToken = default)
  {
    if (IsLoading) return;
    BeginLoad();
    try
    {
      var page = (await service.FetchDetailAsync(pageId, cancellationToken).ConfigureAwait(true)).LandingPage;
      ApplySelected(page);
      CompleteLoad();
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
    }
  }

  private string? SelectStagedPageId(IReadOnlyList<LandingPage> stagedPages)
  {
    if (!string.IsNullOrWhiteSpace(initialSlug))
    {
      return stagedPages.FirstOrDefault(page =>
          string.Equals(page.Slug, initialSlug, StringComparison.Ordinal))?.Id;
    }
    if (SelectedPage is { } selected)
    {
      return stagedPages.FirstOrDefault(page =>
          string.Equals(page.Id, selected.Id, StringComparison.Ordinal))?.Id;
    }
    return stagedPages.FirstOrDefault(page => page.IsDefault)?.Id
        ?? (stagedPages.Count > 0 ? stagedPages[0].Id : null);
  }

  private async Task RefreshPagesAsync(CancellationToken cancellationToken)
  {
    var response = await service.FetchPagesAsync(cancellationToken).ConfigureAwait(true);
    Pages = response.Results.Select(LandingPageRow.FromPage).ToArray();
  }

  private async Task LoadSelectedAsync(string pageId, CancellationToken cancellationToken)
  {
    var response = await service.FetchDetailAsync(pageId, cancellationToken).ConfigureAwait(true);
    ApplySelected(response.LandingPage);
  }

  private void BeginLoad()
  {
    State = LoadState.Loading;
    ErrorMessage = null;
  }

  private void CompleteLoad() => State = LoadState.Loaded;

  private bool HandleLoadException(Exception ex)
  {
    if (ex is not (VouchaApiException or HttpRequestException or InvalidOperationException)) return false;
    ErrorMessage = ex.Message;
    State = LoadState.Error;
    return true;
  }

  private static JsonNullableString? NullableString(string? value) =>
      string.IsNullOrWhiteSpace(value) ? JsonNullableString.Null : JsonNullableString.FromString(value);
}
