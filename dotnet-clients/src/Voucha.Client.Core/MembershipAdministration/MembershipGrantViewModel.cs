using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.MembershipAdministration;

public sealed class MembershipGrantViewModel : ObservableObject
{
  private readonly IMembershipAdministrationService service;
  private readonly IUiLocalization localization;
  private IReadOnlyList<MembershipCatalogProduct> products = [];
  private IReadOnlyList<UserSearchResult> results = [];
  private UserSearchResult? selectedUser;
  private string query = string.Empty;
  private MembershipGrantPlanSlug? selectedPlan;
  private MembershipSku? selectedSku;
  private string durationDays = string.Empty;
  private UiText? planError;
  private UiText? searchError;
  private UiText? success;
  private bool plansLoading;
  private bool plansLoaded;
  private bool searching;
  private bool submitting;
  private long generation;

  public MembershipGrantViewModel(IMembershipAdministrationService service, IUiLocalization? localization = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
  }

  public IReadOnlyList<UserSearchResult> Results { get => results; private set => SetProperty(ref results, value); }
  public UserSearchResult? SelectedUser { get => selectedUser; private set { if (SetProperty(ref selectedUser, value)) OnPropertyChanged(nameof(CanSubmit)); } }
  public string Query { get => query; set { value ??= string.Empty; if (!SetProperty(ref query, value)) return; generation++; IsSearching = false; SelectedUser = null; Results = []; } }
  public MembershipGrantPlanSlug? SelectedPlan { get => selectedPlan; set { if (!SetProperty(ref selectedPlan, value)) return; SelectedSku = null; OnPropertyChanged(nameof(AvailableSkus)); OnPropertyChanged(nameof(CanSubmit)); } }
  public MembershipSku? SelectedSku { get => selectedSku; set { if (SetProperty(ref selectedSku, value)) OnPropertyChanged(nameof(CanSubmit)); } }
  public string DurationDays { get => durationDays; set { value ??= string.Empty; if (SetProperty(ref durationDays, value)) OnPropertyChanged(nameof(CanSubmit)); } }
  public IReadOnlyList<MembershipSku> AvailableSkus => SelectedPlan is { } plan
      ? products.Where(product => string.Equals(product.Plan, plan.ToString(), StringComparison.OrdinalIgnoreCase))
          .SelectMany(product => product.StripeSkus)
          .Where(sku => string.Equals(sku.Plan, plan.ToString(), StringComparison.OrdinalIgnoreCase)).ToArray() : [];
  public bool IsPlansLoading { get => plansLoading; private set { if (SetProperty(ref plansLoading, value)) OnPropertyChanged(nameof(CanSubmit)); } }
  public bool IsSearching { get => searching; private set => SetProperty(ref searching, value); }
  public bool IsSubmitting { get => submitting; private set { if (SetProperty(ref submitting, value)) OnPropertyChanged(nameof(CanSubmit)); } }
  public bool CanSubmit => HasValidSelection && !IsSubmitting && !IsPlansLoading;
  public UiText? PlanError { get => planError; private set => SetError(ref planError, value, nameof(PlanError)); }
  public UiText? SearchError { get => searchError; private set => SetError(ref searchError, value, nameof(SearchError)); }
  public UiText? Error => SearchError ?? PlanError;
  public UiText? Success { get => success; private set => SetProperty(ref success, value); }
  public string? LocalizedError => Error is { } text ? localization.Resolve(text) : null;

  public Task LoadPlansAsync(CancellationToken cancellationToken = default) => LoadPlansAsync(false, cancellationToken);

  private async Task LoadPlansAsync(bool force, CancellationToken cancellationToken)
  {
    if ((!force && plansLoaded) || IsPlansLoading) return;
    IsPlansLoading = true; PlanError = null;
    try
    {
      products = (await service.FetchPlansAsync(cancellationToken).ConfigureAwait(true)).Products;
      plansLoaded = true;
      if (SelectedSku is { } selected)
      {
        SelectedSku = AvailableSkus.FirstOrDefault(sku => string.Equals(sku.Id, selected.Id, StringComparison.Ordinal));
      }
      OnPropertyChanged(nameof(AvailableSkus));
      OnPropertyChanged(nameof(CanSubmit));
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      plansLoaded = false;
      PlanError = Failure(ex, UiMessageKey.NativeSwiftMembershipPlansLoadFailure);
    }
    finally { IsPlansLoading = false; }
  }

  // Caps fan-out from a single search while still following page_info instead of silently
  // truncating at page one.
  private const int SearchPageLimit = 10;
  private const int SearchMaxAdditionalPages = 4;

  public async Task SearchAsync(CancellationToken cancellationToken = default)
  {
    var requested = Query.Trim(); var current = ++generation; SearchError = null; Success = null;
    if (requested.Length == 0) { Results = []; return; }
    IsSearching = true;
    try
    {
      var matches = new List<UserSearchResult>();
      string? after = null;
      for (var page = 0; page <= SearchMaxAdditionalPages; page++)
      {
        if (current != generation) return;
        var response = await service.SearchUsersAsync(
            new SearchUsersRequest(requested, after, SearchPageLimit), cancellationToken).ConfigureAwait(true);
        matches.AddRange(response.Results);
        if (matches.Count >= SearchPageLimit || !response.PageInfo.HasNextPage || response.PageInfo.EndCursor is not { } endCursor)
        {
          break;
        }

        after = endCursor;
      }

      if (current == generation) Results = matches;
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException) { if (current == generation) { Results = []; SearchError = Failure(ex, UiMessageKey.NativeSwiftMembershipSearchFailure); } }
    finally { if (current == generation) IsSearching = false; }
  }

  public void SelectUser(UserSearchResult user)
  {
    ArgumentNullException.ThrowIfNull(user);
    generation++;
    IsSearching = false;
    SelectedUser = user;
    Results = [];
    var label = !string.IsNullOrWhiteSpace(user.Username)
        ? user.Username
        : !string.IsNullOrWhiteSpace(user.Name) ? user.Name : user.Id;
    SetProperty(ref query, label, nameof(Query));
  }

  public async Task<bool> GrantAsync(CancellationToken cancellationToken = default)
  {
    if (IsSubmitting) return false;
    Success = null;
    if (!CanSubmit) { SearchError = UiText.Localized(UiMessageKey.NativeSwiftMembershipMembershipGrantValidation); return false; }
    var submission = new GrantMembershipBody(SelectedUser!.Id, SelectedPlan!.Value, SelectedSku!.Id, ValidDurationDays!.Value);
    IsSubmitting = true; SearchError = null;
    try
    {
      await LoadPlansAsync(true, cancellationToken).ConfigureAwait(true);
      if (PlanError is not null) return false;
      if (!HasAvailableSku(submission.Plan, submission.SkuId))
      {
        SearchError = UiText.Localized(UiMessageKey.NativeSwiftMembershipMembershipGrantValidation);
        return false;
      }
      await service.GrantAsync(submission, cancellationToken).ConfigureAwait(true);
      generation++; Query = string.Empty; SelectedUser = null; SelectedPlan = null; SelectedSku = null; DurationDays = string.Empty; Results = [];
      Success = UiText.Localized(UiMessageKey.NativeSwiftMembershipMembershipGrantSuccess);
      return true;
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException) { SearchError = Failure(ex, UiMessageKey.NativeSwiftMembershipMembershipGrantFailure); return false; }
    finally { IsSubmitting = false; }
  }

  private static UiText Failure(Exception error, UiMessageKey fallback)
  {
    if (error is VouchaApiException { StatusCode: { } status, ApiMessage: { } message }
        && (int)status is >= 400 and < 500
        && !string.IsNullOrWhiteSpace(message))
    {
      return UiText.UserContent(message);
    }

    return UiText.Localized(fallback);
  }

  private bool HasValidSelection =>
      SelectedUser is not null
      && SelectedPlan is not null
      && SelectedSku is not null
      && ValidDurationDays is not null
      && HasAvailableSku(SelectedPlan.Value, SelectedSku.Id);

  private bool HasAvailableSku(MembershipGrantPlanSlug plan, string skuId) =>
      products.Where(product => string.Equals(product.Plan, plan.ToString(), StringComparison.OrdinalIgnoreCase))
          .SelectMany(product => product.StripeSkus)
          .Any(sku => string.Equals(sku.Id, skuId, StringComparison.Ordinal) &&
                      string.Equals(sku.Plan, plan.ToString(), StringComparison.OrdinalIgnoreCase));

  private int? ValidDurationDays => int.TryParse(DurationDays, out var duration)
      && duration is >= 1 and <= 3660 ? duration : null;

  private void SetError(ref UiText? field, UiText? value, string propertyName)
  {
    if (!SetProperty(ref field, value, propertyName)) return;
    OnPropertyChanged(nameof(Error));
    OnPropertyChanged(nameof(LocalizedError));
  }
}
