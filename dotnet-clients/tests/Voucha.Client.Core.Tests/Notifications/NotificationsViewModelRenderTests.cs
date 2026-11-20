using Voucha.Client.Core.Api;
using Voucha.Client.Core.Notifications;
using Xunit;

namespace Voucha.Client.Core.Tests.Notifications;

public sealed class NotificationsViewModelRenderTests
{
  private const string UsDmca = "\"copyright_notice_id\":\"cn1\"";
  private const string Hydration =
      "\"copyright_notices\":{\"cn1\":{\"id\":\"cn1\",\"jurisdiction\":\"us_dmca\"}}";

  [Fact]
  public async Task RowsRenderByKindWithHydrationAndConfiguredWebOrigin()
  {
    var config = new AppConfig(new Uri("https://api.example"), WebBaseUrl: new Uri("https://web.example"));
    var viewModel = new NotificationsViewModel(Service(), appConfig: config);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Items);
    Assert.Equal("copyright_claimant_decision_notice", row.Kind);
    Assert.Equal("Copyright notice decision", row.LocalizedTitle);
    Assert.Contains("https://web.example/copyright/notices/new", row.LocalizedBody, StringComparison.Ordinal);
    Assert.True(row.HasBody);
  }

  [Fact]
  public async Task RowsUseTheDefaultWebOriginWithoutConfig()
  {
    var viewModel = new NotificationsViewModel(Service());

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains(
        "https://voucha.ai/copyright/designated-agent",
        viewModel.Items[0].LocalizedBody,
        StringComparison.Ordinal);
  }

  private static StubService Service() => new(NotificationRenderSupport.Response(
      "copyright_claimant_decision_notice", UsDmca, Hydration) with
  {
    Results = [new EntityReference(null, "n", "n", null, null, null, null, null, null, null, null)],
  });

  private sealed class StubService(NotificationsResponse response) : INotificationsService
  {
    public Task<NotificationsResponse> FetchAsync(
        FetchNotificationsRequest request,
        CancellationToken cancellationToken = default) => Task.FromResult(response);

    public Task MarkReadAsync(string notificationId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<NotificationRedirectTargetResponse> FetchRedirectTargetAsync(
        string notificationId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task MarkAllReadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
