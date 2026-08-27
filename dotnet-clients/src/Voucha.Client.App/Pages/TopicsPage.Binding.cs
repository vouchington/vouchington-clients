using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.App.Pages;

public sealed class TopicsPageBinding : LoadablePageBinding<TopicRow>
{
  private readonly TopicsViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private bool isSessionChangedAttached;

  public TopicsPageBinding(TopicsViewModel viewModel, ISessionStore sessionStore) : base(() => viewModel.LoadAsync())
  {
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAfterSessionChangedAsync());
    viewModel.PropertyChanged += (_, _) =>
    {
      NotifyLoadStateChanged();
      OnPropertyChanged(nameof(SearchQuery));
    };
  }

  public void AttachSessionChanged()
  {
    if (isSessionChangedAttached) return;
    sessionStore.SessionChanged += sessionChangedHandler;
    isSessionChangedAttached = true;
  }

  public void DetachSessionChanged()
  {
    if (!isSessionChangedAttached) return;
    sessionStore.SessionChanged -= sessionChangedHandler;
    isSessionChangedAttached = false;
  }

  public bool CanVote => sessionStore.Current.CanCastPublicVotes();

  public bool CanClearVote => sessionStore.Current.IsAuthenticated && !sessionStore.Current.CanCastPublicVotes();

  public bool CanManageTopics => sessionStore.Current.CanManageTopics();

  public bool CanImportExport => sessionStore.Current.IsAuthenticated;

  public string SearchQuery
  {
    get => viewModel.SearchQuery;
    set => viewModel.SearchQuery = value;
  }

  public override IReadOnlyList<TopicRow> Items => viewModel.Items;

  public override bool HasError => viewModel.HasError;

  public override string? ErrorMessage => viewModel.ErrorMessage;

  protected override bool IsLoading => viewModel.IsLoading;

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Session refresh failures are logged and surfaced by the page state.")]
  private async Task RefreshAfterSessionChangedAsync()
  {
    OnPropertyChanged(nameof(CanVote));
    OnPropertyChanged(nameof(CanClearVote));
    OnPropertyChanged(nameof(CanManageTopics));
    OnPropertyChanged(nameof(CanImportExport));
    try
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
