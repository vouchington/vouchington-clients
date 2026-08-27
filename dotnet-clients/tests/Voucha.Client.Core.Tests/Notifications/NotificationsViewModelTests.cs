using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Notifications;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Notifications;

public sealed class NotificationsViewModelTests
{
  [Fact]
  public async Task LoadAsyncBuildsRowsInResultOrder()
  {
    var service = new RecordingNotificationsService(Page(["n2", "n1"], false));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.False(viewModel.HasMore);
    Assert.Collection(
        viewModel.Items,
        item => Assert.Equal("n2", item.Id),
        item => Assert.Equal("n1", item.Id));
  }

  [Fact]
  public void InitialStateExposesEmptyFlags()
  {
    var viewModel = new NotificationsViewModel(new RecordingNotificationsService());

    Assert.False(viewModel.CanMarkAllRead);
    Assert.False(viewModel.IsLoading);
    Assert.False(viewModel.HasError);
    Assert.True(viewModel.HasMore);
  }

  [Fact]
  public async Task LoadNextPageAsyncForwardsCursorAndAppendsRows()
  {
    var service = new RecordingNotificationsService(
        Page(["n1"], true, "cursor-1"),
        Page(["n2"], false));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);

    Assert.Equal("cursor-1", service.Requests[1].After);
    Assert.Equal(["n1", "n2"], viewModel.Items.Select(item => item.Id));
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public async Task PaginationErrorPreservesExistingItems()
  {
    var service = new RecordingNotificationsService(Page(["n1"], true, "cursor-1"))
    {
      ThrowOnSecondFetch = true,
    };
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Collection(viewModel.Items, item => Assert.Equal("n1", item.Id));
    Assert.Equal("Notifications unavailable.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task CanceledInitialLoadReturnsIdleWithoutError()
  {
    var viewModel = new NotificationsViewModel(new RecordingNotificationsService { CancelFetch = true });

    await viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.False(viewModel.HasError);
    Assert.Empty(viewModel.Items);
  }

  [Fact]
  public async Task CanceledPaginationPreservesLoadedItems()
  {
    var service = new RecordingNotificationsService(Page(["n1"], true, "cursor-1"))
    {
      CancelSecondFetch = true,
    };
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Collection(viewModel.Items, item => Assert.Equal("n1", item.Id));
  }

  [Fact]
  public async Task ReloadAsyncClearsPaginationAndLocalReadOverlay()
  {
    var service = new RecordingNotificationsService(
        Page(["n1"], false),
        Page(["n2"], false));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.MarkReadAsync("n1", TestContext.Current.CancellationToken);
    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Collection(viewModel.Items, item =>
    {
      Assert.Equal("n2", item.Id);
      Assert.False(item.IsRead);
    });
    Assert.Null(service.Requests[1].After);
  }

  [Fact]
  public async Task MarkReadAsyncOptimisticallyUpdatesAndRollsBackOnError()
  {
    var service = new RecordingNotificationsService(Page(["n1"], false)) { FailMarkRead = true };
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.MarkReadAsync("n1", TestContext.Current.CancellationToken);

    Assert.False(viewModel.Items[0].IsRead);
    Assert.Equal("Mark read failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task MarkReadAsyncIgnoresBlankIds()
  {
    var service = new RecordingNotificationsService(Page(["n1"], false));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.MarkReadAsync(" ", TestContext.Current.CancellationToken);

    Assert.Empty(service.MarkReadIds);
    Assert.False(viewModel.Items[0].IsRead);
  }

  [Fact]
  public async Task MarkReadAsyncRollsBackOnCancellation()
  {
    var service = new RecordingNotificationsService(Page(["n1"], false)) { CancelMarkRead = true };
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.MarkReadAsync("n1", TestContext.Current.CancellationToken);

    Assert.False(viewModel.Items[0].IsRead);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task ActivateAsyncMarksUnreadRowsBeforeReturningTrimmedTargetPath()
  {
    var service = new RecordingNotificationsService(
        Page(["n1"], false, targetPathOverride: " /discussion/post-1 "));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var targetPath = await viewModel.ActivateAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Equal("/discussion/post-1", targetPath);
    Assert.Equal(["n1"], service.MarkReadIds);
    Assert.True(viewModel.Items[0].IsRead);
  }

  [Fact]
  public async Task ActivateAsyncMarksUnreadRowsEvenWhenTargetPathIsBlank()
  {
    var service = new RecordingNotificationsService(
        Page(["n1"], false, targetPathOverride: "   "));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var targetPath = await viewModel.ActivateAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Null(targetPath);
    Assert.Equal(["n1"], service.MarkReadIds);
    Assert.True(viewModel.Items[0].IsRead);
  }

  [Fact]
  public async Task ActivateAsyncReturnsNullWhenMarkReadRollsBack()
  {
    var service = new RecordingNotificationsService(
        Page(["n1"], false, targetPathOverride: "/discussion/post-1"))
    {
      FailMarkRead = true,
    };
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var targetPath = await viewModel.ActivateAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Null(targetPath);
    Assert.Equal(["n1"], service.MarkReadIds);
    Assert.False(viewModel.Items[0].IsRead);
  }

  [Fact]
  public async Task ActivateAsyncResolvesNotificationRedirectTarget()
  {
    var service = new RecordingNotificationsService(
        Page(["n1"], false, targetPathOverride: "/prefix/notification-redirect?notification_id=n%261"))
    {
      RedirectTargets = { ["n&1"] = "/rss-feed-items/item-1" },
    };
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var targetPath = await viewModel.ActivateAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.Equal("/rss-feed-items/item-1", targetPath);
    Assert.Equal(["n&1"], service.RedirectTargetIds);
  }

  [Fact]
  public async Task MarkAllReadAsyncDoesNothingWhenEmpty()
  {
    var service = new RecordingNotificationsService();
    var viewModel = new NotificationsViewModel(service);

    await viewModel.MarkAllReadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, service.MarkAllReadCount);
  }

  [Fact]
  public async Task MarkAllReadAsyncRollsBackWithoutClearingPreexistingReads()
  {
    var service = new RecordingNotificationsService(Page(["n1", "n2"], false)) { FailMarkAll = true };
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.MarkReadAsync("n1", TestContext.Current.CancellationToken);
    await viewModel.MarkAllReadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsRead);
    Assert.False(viewModel.Items[1].IsRead);
  }

  [Fact]
  public async Task MarkAllReadAsyncRollsBackOnCancellation()
  {
    var service = new RecordingNotificationsService(Page(["n1"], false)) { CancelMarkAll = true };
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.MarkAllReadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.Items[0].IsRead);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task StructuredCommunityTargetFallsBackToNotificationsInboxWithoutSidecar()
  {
    var response = Page(["n1"], false, targetEntity: new("community", "community-1"));
    var viewModel = new NotificationsViewModel(new RecordingNotificationsService(response));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("/my/notifications", viewModel.Items[0].TargetPath);
  }

  [Theory]
  [InlineData("en", "Notification")]
  [InlineData("es", "Notificación")]
  [InlineData("fr", "Notification")]
  [InlineData("pt", "Notificação")]
  public async Task BlankNotificationTitlesUseLocalizedFallback(string locale, string expected)
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider(locale));
    var localization = new UiLocalization(controller);
    var viewModel = new NotificationsViewModel(
        new RecordingNotificationsService(Page(["n1"], false, titleOverride: " ")),
        localization,
        controller);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(expected, viewModel.Items[0].LocalizedTitle);
  }

  [Fact]
  public async Task BlankNotificationTitleRefreshesWhileAlreadyVisible()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var viewModel = new NotificationsViewModel(
        new RecordingNotificationsService(Page(["n1"], false, titleOverride: " ")),
        localization,
        controller);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    controller.ApplySavedLocale("es");

    Assert.Equal("Notificación", viewModel.Items[0].LocalizedTitle);
  }

  private static NotificationsResponse Page(
      IReadOnlyList<string> ids,
      bool hasNextPage,
      string? endCursor = null,
      IReadOnlySet<string>? readIds = null,
      string? targetPathOverride = null,
      NotificationTargetEntity? targetEntity = null,
      string? titleOverride = null)
  {
    var notifications = ids.ToDictionary(
        id => id,
        id => new Notification(
            id,
            "post",
            titleOverride ?? $"Notification {id}",
            $"Body {id}",
            new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            readIds?.Contains(id) == true ? new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero) : null,
            targetPathOverride ?? $"/notifications/{id}",
            null,
            null,
            null,
            null,
            null,
            targetEntity,
            null),
        StringComparer.Ordinal);
    var results = ids
        .Select(id => new EntityReference(null, id, id, null, null, null, null, null, null, null, null))
        .ToArray();
    return new NotificationsResponse(
        results,
        new PageInfo(endCursor, hasNextPage, null),
        notifications,
        null);
  }

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }

  private sealed class RecordingNotificationsService : INotificationsService
  {
    private readonly Queue<NotificationsResponse> responses;

    public RecordingNotificationsService(params NotificationsResponse[] responses) =>
        this.responses = new Queue<NotificationsResponse>(responses);

    public bool ThrowOnSecondFetch { get; init; }

    public bool CancelFetch { get; init; }

    public bool CancelSecondFetch { get; init; }

    public bool FailMarkRead { get; init; }

    public bool CancelMarkRead { get; init; }

    public bool FailMarkAll { get; init; }

    public bool CancelMarkAll { get; init; }

    public List<FetchNotificationsRequest> Requests { get; } = [];

    public List<string> MarkReadIds { get; } = [];

    public List<string> RedirectTargetIds { get; } = [];

    public Dictionary<string, string> RedirectTargets { get; } = new(StringComparer.Ordinal);

    public int MarkAllReadCount { get; private set; }

    public Task<NotificationsResponse> FetchAsync(
        FetchNotificationsRequest request,
        CancellationToken cancellationToken = default)
    {
      Requests.Add(request);
      if (CancelFetch || (CancelSecondFetch && Requests.Count == 2))
      {
        return Task.FromCanceled<NotificationsResponse>(new CancellationToken(canceled: true));
      }

      if (ThrowOnSecondFetch && Requests.Count == 2)
      {
        return Task.FromException<NotificationsResponse>(
            new InvalidOperationException("Notifications unavailable."));
      }

      return Task.FromResult(responses.Dequeue());
    }

    public Task MarkReadAsync(string notificationId, CancellationToken cancellationToken = default)
    {
      MarkReadIds.Add(notificationId);
      if (CancelMarkRead)
      {
        return Task.FromCanceled(new CancellationToken(canceled: true));
      }

      return FailMarkRead
          ? Task.FromException(new InvalidOperationException("Mark read failed."))
          : Task.CompletedTask;
    }

    public Task<NotificationRedirectTargetResponse> FetchRedirectTargetAsync(
        string notificationId,
        CancellationToken cancellationToken = default)
    {
      RedirectTargetIds.Add(notificationId);
      return Task.FromResult(new NotificationRedirectTargetResponse(RedirectTargets[notificationId]));
    }

    public Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
      MarkAllReadCount++;
      if (CancelMarkAll)
      {
        return Task.FromCanceled(new CancellationToken(canceled: true));
      }

      return FailMarkAll
          ? Task.FromException(new InvalidOperationException("Mark all failed."))
          : Task.CompletedTask;
    }
  }
}
