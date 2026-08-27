using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.PaymentCards;

public sealed partial class PaymentCardsViewModel
{
  private int topicSearchGeneration;
  private string topicSearchQuery = string.Empty;

  public string TopicSearchQuery
  {
    get => topicSearchQuery;
    set
    {
      value ??= string.Empty;
      if (!Set(ref topicSearchQuery, value)) return;
      unchecked { topicSearchGeneration++; }
      TopicResults = [];
      ClearTopicSearchError();
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Search failures are presentation state.")]
  public async Task SearchTopicsAsync(string query, CancellationToken cancellationToken = default)
  {
    TopicSearchQuery = query;
    var submittedQuery = TopicSearchQuery.Trim();
    var generation = unchecked(++topicSearchGeneration);
    ClearTopicSearchError();
    if (submittedQuery.Length == 0) { TopicResults = []; return; }
    try
    {
      var results = await service.SearchTopicsAsync(submittedQuery, cancellationToken).ConfigureAwait(true);
      if (OwnsTopicSearch(generation, submittedQuery)) TopicResults = results;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (OwnsTopicSearch(generation, submittedQuery) && errorOwner is not PaymentCardsErrorOwner.General)
        SetFailure(
            ex,
            UiMessageKey.NativeDotnetPaymentCardsOperationFailed,
            PaymentCardsErrorOwner.Search);
    }
  }

  private bool OwnsTopicSearch(int generation, string submittedQuery) =>
      generation == topicSearchGeneration &&
      string.Equals(TopicSearchQuery.Trim(), submittedQuery, StringComparison.Ordinal);

  private void ClearTopicSearchError()
  {
    if (errorOwner == PaymentCardsErrorOwner.Search) SetError(null);
  }
}
