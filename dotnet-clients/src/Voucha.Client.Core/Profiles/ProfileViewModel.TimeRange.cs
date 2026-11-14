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
      if (ProfileScope.IsOverview || ProfileScope.Section == NativeUserProfileSection.Posts)
      {
        // Only the range changed: reload the selected history tab instead of resetting it from the scope.
        if (User?.Id is { } id)
          await LoadHistoryAsync(id, SelectedHistoryTab, NextHistoryLoadVersion(), cancellationToken).ConfigureAwait(true);
      }
      else
      {
        await LoadScopeFirstPageAsync(cancellationToken).ConfigureAwait(true);
      }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      HistoryItems = [];
      CollectionErrorMessage = ex.Message;
    }
  }
}
