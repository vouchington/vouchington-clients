using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.Core.Fediverse;

public sealed class FediverseInstanceDetailViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly VouchaApiClient client;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private FediverseInstanceResponse? response;
  private string? errorMessage;

  public FediverseInstanceDetailViewModel(
      VouchaApiClient client,
      IBookmarkService? bookmarks = null,
      IUiLocalization? localization = null,
      ITopicsService? topics = null,
      IUiLocaleController? localeController = null)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.localization = localization ?? UiLocalization.English;
    Actions = new TopicDetailViewModel(
        topics ?? new ApiTopicsService(client),
        bookmarks ?? new ApiBookmarkService(client),
        localization,
        localeController);
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public TopicDetailViewModel Actions { get; }

  public void OnUiLocaleChanged() => OnPropertyChanged(string.Empty);

  public void Dispose()
  {
    localeSubscription?.Dispose();
    Actions.Dispose();
  }

  public string? Name => response?.Topic.Name;
  public string? Description => response?.Topic.Markdown;
  public string Software => Join(response?.Instance?.Software, response?.Instance?.SoftwareVersion);
  public string LocalizedSoftwareLabel => IsClassified ? Software : localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceUnclassified);
  public bool IsClassified => Software.Length > 0;
  public string? Protocol => response?.Instance?.Protocol;
  public string ProtocolLabel => Protocol ?? localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceUnknown);
  public string LocalizedTotalUsers => Number(response?.Instance?.TotalUsers);
  public string LocalizedMonthlyActiveUsers => Number(response?.Instance?.MonthlyActiveUsers);
  public bool? OpenRegistrations => response?.Instance?.OpenRegistrations;
  public FediverseTrustTier TrustTier => FediverseTrust.FromElection(response?.HostnameElection);
  public string LocalizedTrustLabel => localization.Localize(FediverseTrust.MessageKey(TrustTier));
  public string LocalizedRegistrationsLabel => localization.Localize(OpenRegistrations switch
  {
    true => UiMessageKey.NativeSwiftRouteSurfaceOpen,
    false => UiMessageKey.NativeSwiftRouteSurfaceClosed,
    null => UiMessageKey.NativeSwiftRouteSurfaceUnknown,
  });
  public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }

  public async Task LoadAsync(string idOrSlug, CancellationToken cancellationToken = default)
  {
    ErrorMessage = null;
    try
    {
      response = await client.FetchFediverseInstanceAsync(idOrSlug, cancellationToken).ConfigureAwait(true);
      await Actions.LoadAsync(response.Topic.Id, followSource: false, cancellationToken).ConfigureAwait(true);
      OnPropertyChanged(string.Empty);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      response = null;
      ErrorMessage = ex.Message;
      OnPropertyChanged(string.Empty);
    }
  }

  private static string Join(params string?[] values) =>
      string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));

  private string Number(int? value) => value is { } number
      ? localization.FormatNumber(number)
      : localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceUnknown);
}
