using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task RevokeCurrentSessionAsyncDeletesTheSessionAndReturnsLogoutSignal()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var shouldLogout = await viewModel.RevokeSessionAsync(CreateSession(), TestContext.Current.CancellationToken);

    Assert.True(shouldLogout);
    Assert.Equal("session-1", service.LastDeletedAuthSessionId);
    Assert.Empty(viewModel.Sessions);
  }

  [Fact]
  public async Task RevokeAllSessionsAsyncClearsTheSessionsAndReturnsLogoutSignal()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var shouldLogout = await viewModel.RevokeAllSessionsAsync(TestContext.Current.CancellationToken);

    Assert.True(shouldLogout);
    Assert.Equal(1, service.RevokeAuthSessionsCount);
    Assert.Empty(viewModel.Sessions);
  }

  public static AuthSession CreateSession() =>
      new(
          "session-1",
          "device-1",
          "MacBook Pro",
          "Safari",
          "203.0.113.8",
          DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
          DateTimeOffset.Parse("2026-07-01T13:00:00Z"),
          DateTimeOffset.Parse("2026-07-31T12:00:00Z"),
          true);
}
