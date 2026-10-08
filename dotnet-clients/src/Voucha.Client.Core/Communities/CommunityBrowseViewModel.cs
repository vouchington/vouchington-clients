using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityBrowseViewModel : ObservableObject, IUiLocaleChangeListener, IDisposable
{
  private readonly ICommunitiesService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private string query = "";
  private IReadOnlyList<CommunityBrowseRow> results = [];
  private LoadState state = LoadState.Idle;
  private string? errorMessage;

  public CommunityBrowseViewModel(
      ICommunitiesService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void OnUiLocaleChanged() => Results = Results.Select(row => row with { Localization = localization }).ToArray();

  public void Dispose() => localeSubscription?.Dispose();

  public string Query
  {
    get => query;
    set => SetProperty(ref query, value ?? string.Empty);
  }

  public IReadOnlyList<CommunityBrowseRow> Results
  {
    get => results;
    private set => SetProperty(ref results, value);
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value))
      {
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (SetProperty(ref errorMessage, value))
      {
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

  public async Task SearchAsync(CancellationToken cancellationToken = default)
  {
    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var response = await service.SearchAsync(query.Trim(), 25, cancellationToken).ConfigureAwait(true);
      Results = response.Results
          .Select(reference =>
          {
            var community = reference.Id is not null && response.Communities.TryGetValue(reference.Id, out var item) ? item : null;
            if (community is null)
            {
              return null;
            }

            response.CommunityMetrics.TryGetValue(community.Id, out var metrics);
            response.Users.TryGetValue(community.CreatedById, out var owner);
            return CommunityBrowseRow.FromCommunity(community, metrics, owner, localization);
          })
          .Where(row => row is not null)
          .Select(row => row!)
          .ToArray();
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
    }
  }

  private bool HandleLoadException(Exception ex)
  {
    if (ex is not (VouchaApiException or HttpRequestException or InvalidOperationException))
    {
      return false;
    }

    ErrorMessage = ex.Message;
    State = LoadState.Error;
    return true;
  }
}

public sealed record CommunityBrowseRow(
    string Id,
    string Name,
    string Slug,
    int MemberCount,
    int PostCount,
    bool Archived,
    PublicContentProvenance? Provenance = null,
    IUiLocalization? Localization = null)
{
  public string? LocalizedProvenanceLabel => PublicProvenanceLabels.Resolve(Provenance, Localization ?? UiLocalization.English);

  public bool HasProvenance => LocalizedProvenanceLabel is not null;

  public static CommunityBrowseRow FromCommunity(
      Community community, CommunityMetrics? metrics, CommunityOwner? owner, IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(community);

    return new(
        community.Id,
        community.Name,
        community.Slug,
        metrics?.MemberCount ?? 0,
        metrics?.PostCount ?? 0,
        community.ArchivedAt is not null,
        community.Provenance,
        localization);
  }
}
