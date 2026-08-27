using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.ReferralLinks;

namespace Voucha.Client.App.Pages;

public sealed class ReferralLinksPageBinding : LoadablePageBinding<ReferralLinkRow>
{
  private readonly ReferralLinksViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private readonly Func<Task> refresh;
  private bool isSessionChangedAttached;

  public ReferralLinksPageBinding(
      ReferralLinksViewModel viewModel,
      ISessionStore sessionStore,
      Func<Task> refresh) : base(refresh)
  {
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.refresh = refresh;
    sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAfterSessionChangedAsync());
    viewModel.PropertyChanged += (_, _) =>
    {
      NotifyLoadStateChanged();
      OnPropertyChanged(nameof(HasMoreAnalytics));
      OnPropertyChanged(nameof(HasMoreLinks));
      OnPropertyChanged(nameof(PaginationIsLoading));
    };
  }

  public override IReadOnlyList<ReferralLinkRow> Items => viewModel.Items;

  public override bool HasError => viewModel.HasError;

  public override string? ErrorMessage => viewModel.ErrorMessage;

  public bool HasMoreAnalytics => viewModel.HasMoreAnalytics;

  public bool HasMoreLinks => viewModel.HasMoreLinks;

  public bool PaginationIsLoading => viewModel.IsLoading;

  public bool HasAuthenticatedActions => sessionStore.Current.IsAuthenticated;

  protected override bool IsLoading => viewModel.IsLoading;

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

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Session refresh failures are logged without disrupting the current native page.")]
  private async Task RefreshAfterSessionChangedAsync()
  {
    OnPropertyChanged(nameof(HasAuthenticatedActions));
    try
    {
      await refresh().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
