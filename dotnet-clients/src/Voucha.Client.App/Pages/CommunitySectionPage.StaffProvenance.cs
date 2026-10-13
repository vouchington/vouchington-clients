using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private readonly Label staffChannelLabel = new() { AutomationId = "CommunitySectionStaffChannel" };
  private readonly Label staffClientLabel = new() { AutomationId = "CommunitySectionStaffClient" };
  private readonly Label staffVerificationLabel = new() { AutomationId = "CommunitySectionStaffVerification" };

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    sessionStore.SessionChanged += sessionChangedHandler;
    Render();
    if (!string.IsNullOrWhiteSpace(Slug))
    {
      await LoadAsync(Slug).ConfigureAwait(true);
    }
  }

  protected override void OnDisappearing()
  {
    sessionStore.SessionChanged -= sessionChangedHandler;
    base.OnDisappearing();
  }

  private void RenderStaffProvenance()
  {
    var staff = viewModel.Community?.StaffProvenance;
    var isStaffViewer = sessionStore.Current.Identity?.Roles?.Any(role =>
        role is "administrator" or "moderator") == true;
    staffChannelLabel.Text = StaffProvenanceLabels.Channel(staff, isStaffViewer, UiCopy.CurrentLocalization);
    staffClientLabel.Text = StaffProvenanceLabels.Client(staff, isStaffViewer, true, UiCopy.CurrentLocalization);
    staffVerificationLabel.Text = StaffProvenanceLabels.Verification(staff, isStaffViewer, true, UiCopy.CurrentLocalization);
    staffChannelLabel.IsVisible = staffChannelLabel.Text is not null;
    staffClientLabel.IsVisible = staffClientLabel.Text is not null;
    staffVerificationLabel.IsVisible = staffVerificationLabel.Text is not null;
  }
}
