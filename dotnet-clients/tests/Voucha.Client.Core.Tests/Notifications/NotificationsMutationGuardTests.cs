using Voucha.Client.Core.Api;
using Voucha.Client.Core.Notifications;
using Xunit;

namespace Voucha.Client.Core.Tests.Notifications;

public sealed class NotificationsMutationGuardTests
{
  [Fact]
  public async Task MarkReadAsyncSkipsUnknownAndAlreadyReadRows()
  {
    var service = new GuardNotificationsService(Page(["n1"], readIds: new HashSet<string>(StringComparer.Ordinal) { "n1" }));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.MarkReadAsync("missing", TestContext.Current.CancellationToken);
    await viewModel.MarkReadAsync("n1", TestContext.Current.CancellationToken);

    Assert.Equal(0, service.MarkReadCount);
  }

  [Fact]
  public async Task MarkAllReadAsyncSkipsWhenAllRowsAlreadyRead()
  {
    var service = new GuardNotificationsService(Page(["n1"], readIds: new HashSet<string>(StringComparer.Ordinal) { "n1" }));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.MarkAllReadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, service.MarkAllReadCount);
  }

  [Fact]
  public async Task LoadNextPageAsyncRethrowsUnexpectedErrors()
  {
    var viewModel = new NotificationsViewModel(new GuardNotificationsService { ThrowUnexpectedFetch = true });

    var exception = await Assert.ThrowsAsync<ApplicationException>(
        () => viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken));

    Assert.Equal("Unexpected notifications failure.", exception.Message);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task ActivationMarksReadWhilePaginationIsInFlight()
  {
    var service = new GuardNotificationsService(Page(["n1"], hasNextPage: true, targetPath: "/notifications/n1"));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.PendingFetch = new TaskCompletionSource<NotificationsResponse>();
    var pagination = viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);

    var targetPath = await viewModel.ActivateAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    await viewModel.MarkAllReadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("/notifications/n1", targetPath);
    Assert.True(viewModel.Items[0].IsRead);
    Assert.Equal(1, service.MarkReadCount);
    Assert.Equal(0, service.MarkAllReadCount);

    service.PendingFetch.SetResult(Page(["n2"]));
    await pagination;
  }

  [Fact]
  public async Task MarkReadAsyncSkipsWhilePaginationIsInFlight()
  {
    var service = new GuardNotificationsService(Page(["n1"], hasNextPage: true));
    var viewModel = new NotificationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.PendingFetch = new TaskCompletionSource<NotificationsResponse>();
    var pagination = viewModel.LoadNextPageAsync(TestContext.Current.CancellationToken);

    await viewModel.MarkReadAsync("n1", TestContext.Current.CancellationToken);

    Assert.False(viewModel.Items[0].IsRead);
    Assert.Equal(0, service.MarkReadCount);

    service.PendingFetch.SetResult(Page(["n2"]));
    await pagination;
  }

  private static NotificationsResponse Page(
      IReadOnlyList<string> ids,
      IReadOnlySet<string>? readIds = null,
      bool hasNextPage = false,
      string? targetPath = null)
  {
    var notifications = ids.ToDictionary(
        id => id,
        id => new Notification(
            id,
            "post",
            $"Notification {id}",
            "Body",
            new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            readIds?.Contains(id) == true ? new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero) : null,
            targetPath,
            null,
            null,
            null),
        StringComparer.Ordinal);
    return new NotificationsResponse(
        ids.Select(id => new EntityReference(null, id, id, null, null, null, null, null, null, null, null)).ToArray(),
        new PageInfo(null, hasNextPage, null),
        notifications,
        null);
  }

  private sealed class GuardNotificationsService : INotificationsService
  {
    private readonly Queue<NotificationsResponse> responses;

    public GuardNotificationsService(params NotificationsResponse[] responses) =>
        this.responses = new Queue<NotificationsResponse>(responses);

    public bool ThrowUnexpectedFetch { get; init; }

    public TaskCompletionSource<NotificationsResponse>? PendingFetch { get; set; }

    public int MarkReadCount { get; private set; }

    public int MarkAllReadCount { get; private set; }

    public Task<NotificationsResponse> FetchAsync(
        FetchNotificationsRequest request,
        CancellationToken cancellationToken = default) =>
        PendingFetch is not null ? PendingFetch.Task :
        ThrowUnexpectedFetch
            ? Task.FromException<NotificationsResponse>(new ApplicationException("Unexpected notifications failure."))
            : Task.FromResult(responses.Dequeue());

    public Task MarkReadAsync(string notificationId, CancellationToken cancellationToken = default)
    {
      MarkReadCount++;
      return Task.CompletedTask;
    }

    public Task<NotificationRedirectTargetResponse> FetchRedirectTargetAsync(
        string notificationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new NotificationRedirectTargetResponse($"/notifications/{notificationId}"));

    public Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
      MarkAllReadCount++;
      return Task.CompletedTask;
    }
  }
}
