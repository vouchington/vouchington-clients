using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  private PageInfo? candidatePageInfo;
  private int itemLimit;
  private bool isItemLimitReached;

  public bool IsItemLimitReached
  {
    get => isItemLimitReached;
    private set => SetProperty(ref isItemLimitReached, value);
  }

  public int ItemLimit
  {
    get => itemLimit;
    private set
    {
      if (SetProperty(ref itemLimit, value))
      {
        OnPropertyChanged(nameof(CanAddItem));
        OnPropertyChanged(nameof(ItemLimitContext));
      }
    }
  }

  private PageInfo? CandidatePageInfo
  {
    get => candidatePageInfo;
    set
    {
      candidatePageInfo = value;
      OnPropertyChanged(nameof(HasMoreCandidates));
    }
  }

  public bool HasMoreCandidates => CandidatePageInfo?.HasNextPage == true
      && !string.IsNullOrEmpty(CandidatePageInfo.EndCursor);

  public string ItemLimitContext =>
      localization.Format(UiMessageKey.SharedCountLabelFormat, ("count", DraftItemCount), ("unit", "item"))
      + " / " + localization.Localize(UiMessageKey.NativeDotnetCsharpCommunitiesLimit)
      + " " + localization.FormatNumber(ItemLimit);

  private int DraftItemCount => CountItems(DraftItems);

  private static int CountItems(IReadOnlyList<LandingPageItem> items) =>
      items.Sum(item => 1 + (item is LandingPageTopicGroupItem group ? CountItems(group.Entries) : 0));

  private bool CanAddWithinLimit => IsContentEditable && DraftItemCount < ItemLimit;

  public async Task LoadMoreCandidatesAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || !HasMoreCandidates) return;
    BeginLoad();
    try
    {
      var response = await service.FetchCandidatesAsync(
          after: CandidatePageInfo!.EndCursor, cancellationToken: cancellationToken).ConfigureAwait(true);
      Candidates = new LandingPageCandidates(
          response.Candidates.CanCreateLandingPages,
          AppendUnique(Candidates.ProfileLinks, response.Candidates.ProfileLinks, item => item.Id),
          AppendUnique(Candidates.Reviews, response.Candidates.Reviews, item => item.Id),
          AppendUnique(Candidates.ReferralLinks, response.Candidates.ReferralLinks, item => item.Id));
      ItemLimit = response.ItemLimit;
      CandidatePageInfo = response.PageInfo;
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

  private static T[] AppendUnique<T>(IReadOnlyList<T> current, IReadOnlyList<T> next, Func<T, string> id)
  {
    var seen = current.Select(id).ToHashSet(StringComparer.Ordinal);
    return current.Concat(next.Where(item => seen.Add(id(item)))).ToArray();
  }
}
