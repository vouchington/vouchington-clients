using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed class CommunityApplicationDetailViewModel(
    ICommunitiesService service, string community, string applicationId) : ObservableObject
{
  private LoadState state;
  private int revision;
  private CommunityApplication? application;
  private UiText? error;
  public LoadState State { get => state; private set => SetProperty(ref state, value); }
  public CommunityApplication? Application { get => application; private set => SetProperty(ref application, value); }
  public UiText? Error { get => error; private set => SetProperty(ref error, value); }

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    var requestRevision = ++revision;
    Application = null;
    Error = null;
    State = LoadState.Loading;
    try
    {
      var response = await service.FetchApplicationAsync(community, applicationId, cancellationToken).ConfigureAwait(true);
      cancellationToken.ThrowIfCancellationRequested();
      if (requestRevision != revision) return;
      Application = response.CommunityApplication;
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
}
