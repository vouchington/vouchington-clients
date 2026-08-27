using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task NotificationPreferenceMutationPatchesOnlyTheChosenField()
  {
    var service = new FakeSettingsService();
    var viewModel = new NotificationPreferencesViewModel(service, new FixedTimezoneResolver("UTC"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.SetNewsDigestAsync("daily", TestContext.Current.CancellationToken);
    Assert.Equal("daily", service.LastEmailPreferencesUpdateBody!.NewsDigestFrequency);
    Assert.Null(service.LastEmailPreferencesUpdateBody.CommunityDigestFrequency);
  }

  [Fact]
  public async Task TimezoneResolverPersistsOnlyAValidUnsetIanaTimezone()
  {
    var service = new FakeSettingsService();
    var viewModel = new NotificationPreferencesViewModel(service, new FixedTimezoneResolver("America/Los_Angeles"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("America/Los_Angeles", service.LastEmailPreferencesUpdateBody!.ModerationEmailTimezone);
  }

  [Fact]
  public async Task SavedUtcTimezoneIsPreservedWithoutAnInitializationPatch()
  {
    var service = new FakeSettingsService
    {
      EmailPreferences = new(true, "weekly", true, "weekly", "daily", [1], "09:00", "UTC"),
    };
    var viewModel = new NotificationPreferencesViewModel(service, new FixedTimezoneResolver("America/New_York"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("UTC", viewModel.ModerationEmailTimezone);
    Assert.Null(service.LastEmailPreferencesUpdateBody);
  }

  [Fact]
  public async Task InvalidDetectedTimezoneFallsBackWithoutAPatch()
  {
    var service = new FakeSettingsService();
    var viewModel = new NotificationPreferencesViewModel(service, new FixedTimezoneResolver("not/a-timezone"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("America/Los_Angeles", viewModel.ModerationEmailTimezone);
    Assert.Null(service.LastEmailPreferencesUpdateBody);
  }

  [Fact]
  public void ModerationEmailChangesNotifySelectedDaysVisibility()
  {
    var viewModel = new NotificationPreferencesViewModel(new FakeSettingsService());
    var changed = new List<string?>();
    viewModel.ModerationEmailCadence = "selected_days";
    viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

    viewModel.ModerationEmailsEnabled = true;

    Assert.Contains(nameof(NotificationPreferencesViewModel.ShowsSelectedDays), changed);
  }

  [Theory]
  [InlineData("UTC", true)]
  [InlineData("America/Los_Angeles", true)]
  [InlineData("Pacific Standard Time", false)]
  [InlineData("America/Los Angeles", false)]
  [InlineData("not/a-timezone", false)]
  public void TimezoneValidationAcceptsOnlyCanonicalIanaValues(string value, bool expected) =>
      Assert.Equal(expected, SystemNotificationTimezoneResolver.IsIana(value));

  private sealed record FixedTimezoneResolver(string? Value) : INotificationTimezoneResolver
  {
    public string? ResolveIanaTimezone() => Value;
  }
}
