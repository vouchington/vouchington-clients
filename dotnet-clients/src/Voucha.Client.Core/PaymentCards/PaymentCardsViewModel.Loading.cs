using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.PaymentCards;

public sealed partial class PaymentCardsViewModel
{
  public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
      hasLoaded ? Task.CompletedTask : LoadAsync(cancellationToken);

  public Task RetryAsync(CancellationToken cancellationToken = default) =>
      errorOwner == PaymentCardsErrorOwner.Search
          ? SearchTopicsAsync(TopicSearchQuery, cancellationToken)
          : HasContinuationError ? LoadMoreAsync(cancellationToken) : LoadAsync(cancellationToken);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native list failures are presentation state.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading) return;
    var generation = unchecked(++loadGeneration);
    var cardsAtStart = cards.ToDictionary(card => card.Id, StringComparer.Ordinal);
    IsLoadingMore = false;
    IsLoading = true;
    HasContinuationError = false;
    initialLoadError = null;
    SetError(null);
    try
    {
      var response = await service.FetchAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
      if (generation != loadGeneration) return;
      var responseCardIds = response.Results.Select(card => card.Id).ToHashSet(StringComparer.Ordinal);
      var cardsChangedWhileLoading = cards.Where(card =>
          (locallyCreatedCardIds.Contains(card.Id) && !responseCardIds.Contains(card.Id)) ||
          !cardsAtStart.TryGetValue(card.Id, out var original) || card != original);
      ReplaceCards([.. response.Results, .. cardsChangedWhileLoading]);
      locallyCreatedCardIds.ExceptWith(responseCardIds);
      pageInfo = response.PageInfo;
      hasLoaded = true;
      OnPropertyChanged(nameof(HasNextPage));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (generation == loadGeneration)
      {
        SetFailure(ex, UiMessageKey.NativeDotnetPaymentCardsOperationFailed);
        if (!hasLoaded) initialLoadError = ErrorText;
      }
    }
    finally
    {
      if (generation == loadGeneration) IsLoading = false;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Continuation failures preserve visible rows and surface retry state.")]
  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || IsLoadingMore || !pageInfo.HasNextPage || pageInfo.EndCursor is null) return;
    var generation = loadGeneration;
    IsLoadingMore = true;
    HasContinuationError = false;
    SetError(null);
    try
    {
      var response = await service.FetchAsync(pageInfo.EndCursor, cancellationToken: cancellationToken).ConfigureAwait(true);
      if (generation != loadGeneration) return;
      ReplaceCards([.. cards, .. response.Results]);
      pageInfo = response.PageInfo;
      OnPropertyChanged(nameof(HasNextPage));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (generation == loadGeneration)
      {
        HasContinuationError = true;
        SetFailure(ex, UiMessageKey.NativeDotnetPaymentCardsOperationFailed);
      }
    }
    finally
    {
      if (generation == loadGeneration) IsLoadingMore = false;
    }
  }

  private PaymentCardOption[] ParentCandidatesFor(PaymentCardDraft currentDraft)
  {
    var candidates = cards.Select(card => PaymentCardOption.From(card, localization)).ToList();
    var edited = currentDraft.Original;
    var liveParentId = currentDraft.AuthorizedUserOfId;
    if (edited.AuthorizedUserOfCard is { } current &&
        liveParentId == current.Id &&
        !deletedCardIds.Contains(current.Id) &&
        candidates.All(item => item.Id != current.Id))
      candidates.Add(PaymentCardOption.From(current, localization));
    var filtered = candidates
        .Where(item => item.Id != edited.Id && (!item.IsClosed || item.Id == liveParentId))
        .GroupBy(item => item.Id, StringComparer.Ordinal).Select(group => group.First());
    return [PaymentCardOption.None(localization), .. filtered];
  }
}
