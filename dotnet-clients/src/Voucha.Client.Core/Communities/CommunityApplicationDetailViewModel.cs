using System.ComponentModel;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed class CommunityApplicationDetailViewModel(
    ICommunitiesService service, string community, string applicationId) : ObservableObject, IDisposable
{
  private LoadState state;
  private readonly CancellationTokenSource authorityCancellation = new();
  private Func<bool>? isOwnerCurrent;
  private Action? detachOwner;
  private bool disposed;
  private int revision;
  private CommunityApplication? application;
  private UiText? error;
  public LoadState State { get => state; private set => SetProperty(ref state, value); }
  public CommunityApplication? Application { get => application; private set => SetProperty(ref application, value); }
  public UiText? Error { get => error; private set => SetProperty(ref error, value); }

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (disposed || authorityCancellation.IsCancellationRequested || isOwnerCurrent?.Invoke() == false)
    {
      Invalidate();
      return;
    }
    using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, authorityCancellation.Token);
    var requestToken = requestCancellation.Token;
    var requestRevision = ++revision;
    Application = null;
    Error = null;
    State = LoadState.Loading;
    try
    {
      var response = await service.FetchApplicationAsync(community, applicationId, requestToken).ConfigureAwait(true);
      requestToken.ThrowIfCancellationRequested();
      if (requestRevision != revision) return;
      if (isOwnerCurrent?.Invoke() == false) { Invalidate(); return; }
      if (response.CommunityApplication is not { } selected ||
          !string.Equals(selected.Id, applicationId, StringComparison.Ordinal))
      {
        Error = UiText.Localized(UiMessageKey.NativeSwiftModerationReportsActionFailed);
        State = LoadState.Error;
        return;
      }
      Application = selected;
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (requestRevision != revision) return;
      State = LoadState.Idle;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException or JsonException)
    {
      if (requestRevision != revision) return;
      Error = UiText.Verbatim(ex.Message);
      State = LoadState.Error;
    }
  }

  internal void AttachOwner(CommunityDetailViewModel owner, Func<bool> isCurrent)
  {
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(isCurrent);
    isOwnerCurrent = isCurrent;
    void Changed(object? sender, PropertyChangedEventArgs args)
    {
      if (!isCurrent()) Invalidate();
      else OnPropertyChanged(nameof(Application));
    }
    owner.PropertyChanged += Changed;
    owner.ApplicationDetailContextInvalidated += Invalidate;
    detachOwner = () =>
    {
      owner.PropertyChanged -= Changed;
      owner.ApplicationDetailContextInvalidated -= Invalidate;
    };
    if (!isCurrent()) Invalidate();
  }

  private void Invalidate()
  {
    ++revision;
    if (!disposed) authorityCancellation.Cancel();
    Application = null;
    Error = null;
    State = LoadState.Idle;
  }

  public void Dispose()
  {
    if (disposed) return;
    Invalidate();
    disposed = true;
    detachOwner?.Invoke();
    detachOwner = null;
    isOwnerCurrent = null;
    authorityCancellation.Dispose();
  }

}
