using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class SupportThreadViewModelPaginationTests
{
  [Fact]
  public async Task SupportThreadViewModelLoadsMoreMessagesWithCursor()
  {
    var service = new FakeChatService
    {
      SupportThreadDetailResult = new SupportThreadDetailResponse(
          new SupportThread(
              "thread-1",
              "contact-1",
              "Need help",
              "conversation-1",
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              DateTimeOffset.Parse("2026-07-01T10:05:00Z"),
              null,
              null,
              null,
              null,
              SupportThreadStatus.Open),
          [
            new SupportMessage(
                "support-message-1",
                "thread-1",
                "inbound",
                "Initial note",
                "<p>Initial note</p>",
                DateTimeOffset.Parse("2026-07-01T10:01:00Z"),
                "user-1",
                DateTimeOffset.Parse("2026-07-01T10:01:00Z"),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null),
          ],
          new PageInfo("cursor-1", true, "support-message-1")),
    };
    var viewModel = new SupportThreadViewModel(service);
    await viewModel.LoadAsync("thread-1", TestContext.Current.CancellationToken);

    service.SupportThreadDetailResult = new SupportThreadDetailResponse(
        service.SupportThreadDetailResult.Thread,
        [
          new SupportMessage(
              "support-message-2",
              "thread-1",
              "outbound",
              "Older note",
              "<p>Older note</p>",
              DateTimeOffset.Parse("2026-07-01T09:59:00Z"),
              "user-2",
              DateTimeOffset.Parse("2026-07-01T10:02:00Z"),
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              null),
        ],
        new PageInfo(null, false, "support-message-2"));

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal("cursor-1", service.LastSupportThreadAfter);
    Assert.Equal(25, service.LastSupportThreadLimit);
    Assert.False(viewModel.HasMoreMessages);
    Assert.Equal(["Older note", "Initial note"], viewModel.Messages.Select(message => message.UserContent));
  }

  [Fact]
  public async Task SupportThreadViewModelRetriesDetailLoadAfterLoadMoreFailure()
  {
    var service = new FakeChatService
    {
      SupportThreadDetailResult = new SupportThreadDetailResponse(
          new SupportThread(
              "thread-1",
              "contact-1",
              "Need help",
              "conversation-1",
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              DateTimeOffset.Parse("2026-07-01T10:05:00Z"),
              null,
              null,
              null,
              null,
              SupportThreadStatus.Open),
          [
            new SupportMessage(
                "support-message-1",
                "thread-1",
                "inbound",
                "Initial note",
                "<p>Initial note</p>",
                DateTimeOffset.Parse("2026-07-01T10:01:00Z"),
                "user-1",
                DateTimeOffset.Parse("2026-07-01T10:01:00Z"),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null),
          ],
          new PageInfo("cursor-1", true, "support-message-1")),
    };
    var viewModel = new SupportThreadViewModel(service);

    await viewModel.LoadAsync("thread-1", TestContext.Current.CancellationToken);

    service.FetchSupportThreadError = new InvalidOperationException("load more failed");
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);

    service.FetchSupportThreadError = null;
    service.SupportThreadDetailResult = new SupportThreadDetailResponse(
        service.SupportThreadDetailResult.Thread,
        [
          new SupportMessage(
              "support-message-2",
              "thread-1",
              "outbound",
              "Fresh detail",
              "<p>Fresh detail</p>",
              DateTimeOffset.Parse("2026-07-01T10:02:00Z"),
              "user-2",
              DateTimeOffset.Parse("2026-07-01T10:02:00Z"),
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              null),
        ],
        new PageInfo(null, false, "support-message-2"));

    await viewModel.LoadAsync("thread-1", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.False(viewModel.HasError);
    Assert.Single(viewModel.Messages);
    Assert.Equal("Fresh detail", viewModel.Messages[0].UserContent);
  }

  [Fact]
  public async Task SupportThreadViewModelRefreshesMessagesAndThreadStateOnRepeatLoad()
  {
    var service = new FakeChatService
    {
      SupportThreadDetailResult = new SupportThreadDetailResponse(
          new SupportThread(
              "thread-1",
              "contact-1",
              "Need help",
              "conversation-1",
              DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
              DateTimeOffset.Parse("2026-07-01T10:05:00Z"),
              null,
              null,
              null,
              null,
              SupportThreadStatus.Open),
          [
            new SupportMessage(
                "support-message-1",
                "thread-1",
                "inbound",
                "Initial note",
                "<p>Initial note</p>",
                DateTimeOffset.Parse("2026-07-01T10:01:00Z"),
                "user-1",
                DateTimeOffset.Parse("2026-07-01T10:01:00Z"),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null),
          ],
          new PageInfo("support-message-1", true, null)),
    };
    var viewModel = new SupportThreadViewModel(service);

    await viewModel.LoadAsync("thread-1", TestContext.Current.CancellationToken);
    service.SupportThreadDetailResult = new SupportThreadDetailResponse(
        new SupportThread(
            "thread-1",
            "contact-1",
            "Updated help",
            "conversation-1",
            DateTimeOffset.Parse("2026-07-01T10:00:00Z"),
            DateTimeOffset.Parse("2026-07-01T10:06:00Z"),
            null,
            null,
            null,
            null,
            SupportThreadStatus.Resolved),
        [
          new SupportMessage(
              "support-message-2",
              "thread-1",
              "outbound",
              "Fresh note",
              "<p>Fresh note</p>",
              DateTimeOffset.Parse("2026-07-01T10:02:00Z"),
              "user-2",
              DateTimeOffset.Parse("2026-07-01T10:02:00Z"),
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              null,
              null),
        ],
        new PageInfo(null, false, null));

    await viewModel.LoadAsync("thread-1", TestContext.Current.CancellationToken);

    Assert.Equal(2, service.FetchSupportThreadCount);
    Assert.Equal("Updated help", viewModel.Title);
    Assert.False(viewModel.HasMoreMessages);
    Assert.Equal("resolved", viewModel.Thread?.ProtocolStatus);
    Assert.Equal("Fresh note", viewModel.Messages[0].UserContent);
  }
}
