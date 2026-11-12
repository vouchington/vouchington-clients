using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  public PostTimeRangeSelection TimeRange { get; }

  public async Task SelectTimeRangeAsync(string value, CancellationToken cancellationToken = default)
  {
    if (!TimeRange.Select(value)) return;
    try
    {
      CollectionErrorMessage = null;
      await LoadScopeFirstPageAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      CollectionErrorMessage = ex.Message;
    }
  }
}
