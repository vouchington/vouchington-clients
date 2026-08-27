using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  private UiText? sourceCrawlHistoryAccessError;
  private UiText? sourceCrawlHistoryRequestError;
  private string? sourceCrawlHistoryRetryId;

  public string? SourceCrawlHistoryAccessErrorMessage => sourceCrawlHistoryAccessError is { } error
      ? localization.Resolve(error)
      : null;

  public bool HasSourceCrawlHistoryAccessError => SourceCrawlHistoryAccessErrorMessage is not null;

  public string? SourceCrawlHistoryRequestErrorMessage => sourceCrawlHistoryRequestError is { } error
      ? localization.Resolve(error)
      : null;

  public bool HasSourceCrawlHistoryRequestError => SourceCrawlHistoryRequestErrorMessage is not null;

  public async Task RetrySourceCrawlHistoryAsync(CancellationToken cancellationToken = default)
  {
    if (sourceCrawlHistoryRequestError is null) return;
    if (sourceCrawlHistoryRetryId is { } crawlId)
    {
      await LoadSourceCrawlAsync(crawlId, cancellationToken).ConfigureAwait(true);
      return;
    }
    await LoadSourceCrawlsAsync(cancellationToken).ConfigureAwait(true);
  }

  private void SetSourceCrawlHistoryRequestError(string? crawlId)
  {
    sourceCrawlHistoryRetryId = crawlId;
    sourceCrawlHistoryRequestError = UiText.Localized(UiMessageKey.NativeDotnetCsharpActionFailed);
    OnPropertyChanged(nameof(SourceCrawlHistoryRequestErrorMessage));
    OnPropertyChanged(nameof(HasSourceCrawlHistoryRequestError));
  }

  private void ClearSourceCrawlHistoryRequestError()
  {
    sourceCrawlHistoryRetryId = null;
    if (sourceCrawlHistoryRequestError is null) return;
    sourceCrawlHistoryRequestError = null;
    OnPropertyChanged(nameof(SourceCrawlHistoryRequestErrorMessage));
    OnPropertyChanged(nameof(HasSourceCrawlHistoryRequestError));
  }

  private void SetSourceCrawlHistoryAccessError()
  {
    sourceCrawlHistoryAccessError =
        UiText.Localized(UiMessageKey.NativeDotnetResidualCrawlHistoryPaidAccessRequired);
    OnPropertyChanged(nameof(SourceCrawlHistoryAccessErrorMessage));
    OnPropertyChanged(nameof(HasSourceCrawlHistoryAccessError));
  }

  private void ClearSourceCrawlHistoryAccessError()
  {
    if (sourceCrawlHistoryAccessError is null) return;
    sourceCrawlHistoryAccessError = null;
    OnPropertyChanged(nameof(SourceCrawlHistoryAccessErrorMessage));
    OnPropertyChanged(nameof(HasSourceCrawlHistoryAccessError));
  }
}
