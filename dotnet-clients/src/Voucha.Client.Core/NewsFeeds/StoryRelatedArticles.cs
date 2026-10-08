using System.Collections.ObjectModel;
using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.NewsFeeds;

public sealed class StoryRelatedArticles : INotifyPropertyChanged
{
  private readonly CursorPaginationState<NewsFeedItem, string> pages;
  private readonly IUiLocalization localization;
  private bool isExpanded;

  public StoryRelatedArticles(string storyId, string primaryItemId, IReadOnlyList<NewsFeedItem> preview, PageInfo pageInfo, IUiLocalization localization)
  {
    StoryId = storyId;
    PrimaryItemId = primaryItemId;
    this.localization = localization;
    ArgumentNullException.ThrowIfNull(pageInfo);
    pages = new(item => item.Id, preview.Where(item => item.Id != primaryItemId));
    pages.RestoreContinuation(pageInfo.EndCursor, pageInfo.HasNextPage);
    Items = new(pages.Items);
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  public string StoryId { get; }
  public string PrimaryItemId { get; }
  public ObservableCollection<NewsFeedItem> Items { get; }
  public IReadOnlyList<NewsFeedItem> VisibleItems => Items.Where(item => !item.IsHidden).ToArray();
  public bool HasMore => pages.HasMore;
  public bool IsLoading => pages.IsLoading;
  public bool HasError => pages.LastError is not null;
  public string? ErrorMessage => pages.LastError;
  public bool HasItems => Items.Any(item => !item.IsHidden);
  public bool CanExpand => HasItems || HasMore;
  public bool CanLoadMore => HasMore && !IsLoading;
  public string LoadMoreLabel => localization.Localize(HasError ? UiMessageKey.NativeCommonRetry : UiMessageKey.NativeSwiftCommonLoadMore);
  public string CountLabel => localization.Format(
      HasMore ? UiMessageKey.NativeCommonRelatedArticlesMore : UiMessageKey.NativeCommonRelatedArticles,
      ("count", VisibleItems.Count));

  public bool IsExpanded
  {
    get => isExpanded;
    set
    {
      if (isExpanded == value) return;
      isExpanded = value;
      Notify();
    }
  }

  internal CursorPageRequest? BeginNextPage()
  {
    pages.ReplaceItems(Items);
    var request = pages.BeginNextPage();
    Notify();
    return request;
  }

  internal void InvalidatePendingPage() => pages.InvalidateRequestsPreservingPage();

  internal StoryRelatedArticles WithPrimary(NewsFeedItem primary, IEnumerable<NewsFeedItem> additionalPeers)
  {
    var peers = additionalPeers.Concat(Items).Where(item => item.Id != primary.Id)
        .Select(item => item with { StoryArticles = null });
    return new(StoryId, primary.Id, peers.ToArray(), new(pages.EndCursor, pages.HasMore, null), localization)
    { IsExpanded = IsExpanded };
  }

  internal void IncludePeers(IEnumerable<NewsFeedItem> peers)
  {
    var known = Items.Select(item => item.Id).Append(PrimaryItemId).ToHashSet(StringComparer.Ordinal);
    var additions = peers.Where(item => known.Add(item.Id)).Select(item => item with { StoryArticles = null }).ToArray();
    if (additions.Length == 0) return;
    pages.InvalidateRequestsPreservingPage();
    foreach (var item in additions) Items.Add(item);
    pages.ReplaceItems(Items);
    Notify();
  }

  internal void Complete(CursorPageRequest request, NewsFeedPage page)
  {
    if (!pages.Complete(request, page.Items.Where(item => item.Id != PrimaryItemId), page.PageInfo.EndCursor, page.PageInfo.HasNextPage)) return;
    var known = Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
    foreach (var item in pages.Items.Where(item => known.Add(item.Id))) Items.Add(item);
    Notify();
  }

  internal void Fail(CursorPageRequest request, string message)
  {
    pages.Fail(request, message);
    Notify();
  }

  internal void Cancel(CursorPageRequest request)
  {
    pages.Cancel(request);
    Notify();
  }

  internal void Notify()
  {
    foreach (var name in new[] { nameof(IsExpanded), nameof(HasMore), nameof(IsLoading), nameof(HasError), nameof(ErrorMessage), nameof(HasItems), nameof(VisibleItems), nameof(CanExpand), nameof(CanLoadMore), nameof(LoadMoreLabel), nameof(CountLabel) })
      PropertyChanged?.Invoke(this, new(name));
  }
}

public interface IStoryRelatedArticlesService
{
  Task<NewsFeedPage> GetStoryRelatedArticlesPageAsync(string storyId, string primaryItemId, string? after, CancellationToken cancellationToken = default);
}
