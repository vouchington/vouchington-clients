using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  public bool IsLoading => State == LoadState.Loading;

  public bool IsNotLoading => !IsLoading;

  public bool IsContentEditable => SelectedPage is not null && !IsLoading;

  public bool CanCreatePage => Candidates.CanCreateLandingPages && !IsLoading;
}
