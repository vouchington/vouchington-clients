using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class NotificationPreferencesViewTests
{
  [Fact]
  public async Task RenderedModerationScheduleAppearsAndDisappearsAfterPreferenceMutations()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    var service = new SettingsService();
    var model = new NotificationPreferencesViewModel(service);
    var view = new NotificationPreferencesView { BindingContext = model };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();

    var schedule = Assert.IsType<VerticalStackLayout>(Find<Picker>(view, "notification-cadence").Parent);
    var days = view.FindByName<HorizontalStackLayout>("DaysControls");
    Assert.True(schedule.IsVisible);
    Assert.True(days.IsVisible);

    service.Preferences = service.Preferences with { ModerationEmailsEnabled = false };
    await model.SetModerationEmailsAsync(false, TestContext.Current.CancellationToken);
    Assert.False(schedule.IsVisible);
    Assert.False(days.IsVisible);

    service.Preferences = service.Preferences with { ModerationEmailsEnabled = true };
    await model.SetModerationEmailsAsync(true, TestContext.Current.CancellationToken);
    Assert.True(schedule.IsVisible);
    Assert.True(days.IsVisible);
  }
}
