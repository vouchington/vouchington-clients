using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly ILandingPagesService service;
  private readonly IUiLocalization localization;
  private bool canViewAnalytics;
  private readonly Func<CancellationToken, Task<MembershipResponse>>? fetchMembership;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<LandingPageRow> pages = [];
  private IReadOnlyList<LandingPageItem> draftItems = [];
  private LandingPageCandidates candidates = new(false, [], [], []);
  private LandingPage? selectedPage;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private string? initialSlug;

  public LandingPagesViewModel(
      ILandingPagesService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null,
      bool canViewAnalytics = true,
      Func<CancellationToken, Task<MembershipResponse>>? fetchMembership = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    this.canViewAnalytics = canViewAnalytics;
    this.fetchMembership = fetchMembership;
    InitializePicker();
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<LandingPageRow> Pages
  {
    get => pages;
    private set => SetProperty(ref pages, value);
  }

  public LandingPageCandidates Candidates
  {
    get => candidates;
    private set
    {
      if (SetProperty(ref candidates, value))
      {
        OnPropertyChanged(nameof(CanCreatePage));
        NotifyCandidateState();
      }
    }
  }

  public LandingPage? SelectedPage
  {
    get => selectedPage;
    private set
    {
      if (SetProperty(ref selectedPage, value))
      {
        OnPropertyChanged(nameof(IsContentEditable));
        OnPropertyChanged(nameof(CanAddItem));
        OnPropertyChanged(nameof(IsAnalyticsPaidAccessRequired));
        OnPropertyChanged(nameof(HasSelectedPageAnalyticsSection));
      }
    }
  }

  public IReadOnlyList<LandingPageItem> DraftItems
  {
    get => draftItems;
    private set
    {
      if (SetProperty(ref draftItems, value)) NotifyDraftItemState();
    }
  }

  public void SetInitialSlug(string? slug) =>
      initialSlug = string.IsNullOrWhiteSpace(slug) ? null : slug;

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value))
      {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsNotLoading));
        OnPropertyChanged(nameof(IsContentEditable));
        OnPropertyChanged(nameof(CanCreatePage));
        OnPropertyChanged(nameof(CanAddItem));
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public bool HasError => State == LoadState.Error;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

}
