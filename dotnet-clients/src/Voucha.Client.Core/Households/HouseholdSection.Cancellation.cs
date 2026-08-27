using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Households;

public sealed partial class HouseholdSection
{
  internal void CancelLoad()
  {
    pagination.InvalidateRequestsPreservingPage();
    if (IsLoading) State = LoadState.Loaded;
    OnPropertyChanged(nameof(CanAutomaticallyLoadMembers));
  }

  internal void CancelLoad(CursorPageRequest request)
  {
    if (!pagination.Cancel(request)) return;
    ErrorMessage = null;
    if (IsLoading) State = LoadState.Loaded;
    OnPropertyChanged(nameof(CanAutomaticallyLoadMembers));
  }
}
