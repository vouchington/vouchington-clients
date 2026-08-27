using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Fact]
  public async Task CreateDataRequestAsyncUsesTheCurrentUserAndSetsTheStatus()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);

    await viewModel.CreateDataRequestAsync(TestContext.Current.CancellationToken);

    Assert.Equal(1, service.FetchMyIdentityCount);
    Assert.Equal("user-1", service.LastCreatedUserDataRequestUserId);
    Assert.Equal("request-1", viewModel.DataRequest!.Id);
    Assert.Equal(SettingsViewModelTestSupport.DataRequestStatusText("Ready"), viewModel.LocalizedDataRequestStatus);
  }

  [Fact]
  public async Task RefreshDataRequestAsyncShowsTheEmptyStateWhenNoRequestExists()
  {
    var service = new RecordingSettingsService { UserDataRequestResult = null };
    var viewModel = new SettingsViewModel(service);

    await viewModel.RefreshDataRequestAsync(TestContext.Current.CancellationToken);

    Assert.Equal(1, service.FetchMyIdentityCount);
    Assert.Null(viewModel.DataRequest);
    Assert.Equal("No export request yet", viewModel.LocalizedDataRequestStatus);
  }

  [Fact]
  public async Task DeleteConfirmationRaisesCanDeleteAccountWhenTyped()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var propertyChanged = false;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(SettingsViewModel.CanDeleteAccount))
      {
        propertyChanged = true;
      }
    };

    viewModel.DeleteConfirmation = "  DELETE my account  ";

    Assert.True(propertyChanged);
    Assert.True(viewModel.CanDeleteAccount);
  }

  [Fact]
  public async Task DeleteAccountAsyncRefusesToCallTheServiceBeforeConfirmation()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var deleted = await viewModel.DeleteAccountAsync(TestContext.Current.CancellationToken);

    Assert.False(deleted);
    Assert.Equal(0, service.DeleteUserCallCount);
    Assert.Equal("Type \"delete my account\" to confirm account deletion.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task CreateDataRequestAsyncUsesThePostResponseForTheCurrentUser()
  {
    var service = new RecordingSettingsService();
    var viewModel = new SettingsViewModel(service);

    await viewModel.CreateDataRequestAsync(TestContext.Current.CancellationToken);

    Assert.Equal("user-1", service.LastCreatedUserDataRequestUserId);
    Assert.Equal("request-1", viewModel.DataRequest!.Id);
    Assert.Equal(SettingsViewModelTestSupport.DataRequestStatusText("Ready"), viewModel.LocalizedDataRequestStatus);
  }

  [Fact]
  public async Task ExportDownloadStateTracksTheCurrentRequest()
  {
    var service = new RecordingSettingsService
    {
      UserDataRequestResult = new UserDataRequestResponse(
          Id: "request-1",
          UserId: "user-1",
          Status: "processing",
          CreatedAt: DateTimeOffset.Parse("2026-07-01T16:00:00-07:00"),
          ExpiresAt: null,
          DownloadUrl: new Uri("https://download")),
    };
    var viewModel = new SettingsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.ReportDataRequestDownloadError(new InvalidOperationException("download failed"));

    Assert.Equal(SettingsViewModelTestSupport.DataRequestStatusText("Processing"), viewModel.LocalizedDataRequestStatus);
    Assert.False(viewModel.CanDownloadDataRequest);
    Assert.Null(viewModel.DataRequestDownloadUrl);
    Assert.Equal("Failed to open download link: download failed", viewModel.ErrorMessage);

    service.UserDataRequestResult = new UserDataRequestResponse(
        Id: "request-1",
        UserId: "user-1",
        Status: "ready",
        CreatedAt: DateTimeOffset.Parse("2026-07-01T16:00:00-07:00"),
        ExpiresAt: DateTimeOffset.Parse("2027-07-08T16:00:00-07:00"),
        DownloadUrl: new Uri("https://download"));

    await viewModel.RefreshDataRequestAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanDownloadDataRequest);
    Assert.Equal(new Uri("https://download"), viewModel.DataRequestDownloadUrl);
    Assert.Equal("Failed to open download link: download failed", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ExpiredReadyExportDoesNotExposeDownloadUrl()
  {
    var service = new RecordingSettingsService
    {
      UserDataRequestResult = new UserDataRequestResponse(
          Id: "request-1",
          UserId: "user-1",
          Status: "ready",
          CreatedAt: DateTimeOffset.Parse("2026-07-01T16:00:00-07:00"),
          ExpiresAt: DateTimeOffset.Parse("2026-07-08T16:00:00-07:00"),
          DownloadUrl: new Uri("https://download")),
    };
    var viewModel = new SettingsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanDownloadDataRequest);
    Assert.Null(viewModel.DataRequestDownloadUrl);
  }
}
