using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.App.Pages;

public sealed class TopicsPageBinding : LoadablePageBinding<TopicPageRow>
{
  private readonly TopicsViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly AppConfig config;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private bool isSessionChangedAttached;

  public TopicsPageBinding(TopicsViewModel viewModel, ISessionStore sessionStore, AppConfig config) : base(() => viewModel.LoadAsync())
  {
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.config = config ?? throw new ArgumentNullException(nameof(config));
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
    OnPropertyChanged(nameof(Items));
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

  public override IReadOnlyList<TopicPageRow> Items => viewModel.Items.Select(row => row with
  {
    IsStaffViewer = sessionStore.Current.Identity?.Roles?.Any(role =>
        role is "administrator" or "moderator") == true,
  }).Select(row => TopicPageRow.Create(row, config)).ToArray();

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
    OnPropertyChanged(nameof(Items));
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
