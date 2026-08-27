using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private IReadOnlyList<LandingPageRow> adminLandingPages = [];
  private bool canViewAdminLandingPages;
  private string? adminLandingPagesError;

  public IReadOnlyList<LandingPageRow> AdminLandingPages
  {
    get => adminLandingPages;
    private set => SetProperty(ref adminLandingPages, value);
  }

  public bool CanViewAdminLandingPages
  {
    get => canViewAdminLandingPages;
    private set
    {
      if (SetProperty(ref canViewAdminLandingPages, value)) OnPropertyChanged(nameof(CanShowAdminLandingPages));
    }
  }

  public bool CanShowAdminLandingPages => CanViewAdminLandingPages && IsOverviewScope;

  public string? AdminLandingPagesError
  {
    get => adminLandingPagesError;
    private set
    {
      if (SetProperty(ref adminLandingPagesError, value))
      {
        OnPropertyChanged(nameof(HasAdminLandingPagesError));
      }
    }
  }

  public bool HasAdminLandingPagesError => !string.IsNullOrWhiteSpace(AdminLandingPagesError);

  public async Task LoadAdminLandingPagesAsync(
      string userId,
      bool canView,
      CancellationToken cancellationToken = default)
  {
    CanViewAdminLandingPages = canView;
    AdminLandingPagesError = null;
    if (!canView || string.IsNullOrWhiteSpace(userId))
    {
      AdminLandingPages = [];
      return;
    }

    try
    {
      var response = await landingPagesService.FetchAdminUserLandingPagesAsync(userId, cancellationToken).ConfigureAwait(true);
      AdminLandingPages = response.Results.Select(LandingPageRow.FromPage).ToArray();
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      AdminLandingPages = [];
      AdminLandingPagesError = ex.Message;
    }
  }
}
