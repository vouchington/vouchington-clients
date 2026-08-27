using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Users;

public sealed partial class UsersBrowseViewModel : ObservableObject, IDisposable
{
  private readonly IUsersSearchService service;
  private readonly INavigationViewerProvider viewerProvider;
  private readonly IUiLocalization localization;
  private readonly CursorPaginationState<UserSearchResult, string> pagination = new(user => user.Id);
  private string query = string.Empty;
  private UiText? searchError;
  private bool searching;
  private long generation;

  public UsersBrowseViewModel(
      IUsersSearchService service,
      INavigationViewerProvider viewerProvider,
      IUiLocalization? localization = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.viewerProvider = viewerProvider ?? throw new ArgumentNullException(nameof(viewerProvider));
    this.localization = localization ?? UiLocalization.English;
    viewerProvider.ViewerChanged += OnViewerChanged;
  }

  public IReadOnlyList<UserSearchResult> Results => pagination.Items;
  public string Query { get => query; set { value ??= string.Empty; if (!SetProperty(ref query, value)) return; generation++; IsSearching = false; ResetResults(); } }
  public bool IsSearching { get => searching; private set => SetProperty(ref searching, value); }
  public UiText? SearchError { get => searchError; private set => SetProperty(ref searchError, value); }
  public bool IsAdministrator => viewerProvider.CurrentViewer.Roles.Contains("administrator", StringComparer.Ordinal);

  // Deliberately returns NativeRoutePath, not string. NativeRoutePath.Segments already escapes each
  // segment; converting to string here (as ProfileNavigationTargets.User does) would make callers
  // route through AppShell.OpenNativePathAsync(string) -> OpenEscapedNativePathAsync, which re-escapes
  // an already-escaped segment and double-encodes it. Passing the NativeRoutePath object directly
  // resolves the OpenNativePathAsync(NativeRoutePath) -> OpenEncodedNativePathAsync overload instead,
  // which does not re-escape. Do not "fix" this back to string.
  public static NativeRoutePath AdminTarget(UserSearchResult user)
  {
    ArgumentNullException.ThrowIfNull(user);
    return NativeRoutePath.Segments("user", string.IsNullOrWhiteSpace(user.Username) ? user.Id : user.Username, "admin");
  }

  public void Dispose() => viewerProvider.ViewerChanged -= OnViewerChanged;

  private void OnViewerChanged(object? sender, NavigationViewerChangedEventArgs args) =>
      OnPropertyChanged(nameof(IsAdministrator));

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
}
