using System.ComponentModel;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Friends;

public sealed partial class FriendRecommendationsViewModel :
    INotifyPropertyChanged,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IFriendRecommendationsService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private readonly Dictionary<string, User> users = new(StringComparer.Ordinal);
  private readonly Dictionary<string, RecommendationTombstone> tombstones = new(StringComparer.Ordinal);
  private readonly HashSet<string> mutationsInFlight = new(StringComparer.Ordinal);
  private IReadOnlyList<FriendRecommendation> recommendations = [];
  private IReadOnlyList<FriendRecommendationRow> items = [];
  private string? cursor;
  private bool hasMore;
  private LoadState state;
  private string? errorMessage;
  private int loadRequestId;
  private int activePageLoads;

  public FriendRecommendationsViewModel(
      IFriendRecommendationsService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public IReadOnlyList<FriendRecommendationRow> Items
  {
    get => items;
    private set
    {
      items = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasItems));
    }
  }

  public bool HasItems => Items.Count > 0;

  public bool HasMore
  {
    get => hasMore;
    private set
    {
      if (hasMore == value) return;
      hasMore = value;
      OnPropertyChanged();
    }
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (state == value) return;
      state = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsLoading));
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool IsLoading => State == LoadState.Loading;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (errorMessage == value) return;
      errorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      LoadPageAsync(null, replace: true, cancellationToken);

  public Task ReloadAsync(CancellationToken cancellationToken = default) =>
      LoadPageAsync(null, replace: true, cancellationToken);

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      !HasMore || cursor is null || IsLoading
          ? Task.CompletedTask
          : LoadPageAsync(cursor, replace: false, cancellationToken);

  public Task FollowAsync(
      FriendRecommendationRow row,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    return MutateAsync(row, FriendRecommendationMutation.Follow, cancellationToken);
  }

  public Task DismissAsync(
      FriendRecommendationRow row,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    return MutateAsync(row, FriendRecommendationMutation.Dismiss, cancellationToken);
  }

  public void OnUiLocaleChanged() => PublishRows();

  public void Dispose() => localeSubscription?.Dispose();

  private void PublishRows()
  {
    Items = recommendations
        .Select(recommendation => new FriendRecommendationRow(
            recommendation,
            users.GetValueOrDefault(recommendation.Id),
            localization))
        .ToArray();
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

  private sealed record RecommendationTombstone(
      FriendRecommendation Recommendation,
      int Index,
      bool Succeeded);

  private enum FriendRecommendationMutation
  {
    Follow,
    Dismiss,
  }
}
