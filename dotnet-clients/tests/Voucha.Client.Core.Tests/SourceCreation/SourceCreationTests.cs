using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.SourceCreation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.SourceCreation;

public sealed class SourceCreationTests
{
  [Theory]
  [InlineData("completed")]
  [InlineData("failed")]
  public async Task CanonicalLifecyclePostsOnceAndRetainsFinalIdentity(string terminal)
  {
    var service = new FakeService();
    service.Reads.Enqueue(() => Task.FromResult(Status("pending")));
    service.Reads.Enqueue(() => Task.FromResult(Status(terminal)));
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.Equal(1, service.Posts);
    Assert.Equal(2, service.Gets);
    Assert.False(model.IsBusy);
    Assert.False(model.CanCreate);
    Assert.False(model.CanRetry);
    Assert.Null(model.Error);
    Assert.NotNull(model.Status);
    model.OnUiLocaleChanged();
    Assert.Equal(terminal == "completed", model.CanOpen);
    if (model.CanOpen)
    {
      Assert.Equal(Status(terminal).Rows[0].EntityId, model.EntityId);
      Assert.Equal("resolved-topic", model.TopicSlug);
    }
    await model.CreateAsync();
    await model.RetryAsync();
    Assert.Equal(1, service.Posts);
  }

  [Fact]
  public async Task TransientReadFailureRetriesOnlyGet()
  {
    var service = new FakeService();
    service.Reads.Enqueue(() => throw new HttpRequestException());
    service.Reads.Enqueue(() => Task.FromResult(Status("completed")));
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.NotNull(model.Error);
    Assert.True(model.CanRetry);
    await model.RetryAsync();
    Assert.True(model.CanOpen);
    Assert.Equal(1, service.Posts);
  }

  [Theory]
  [InlineData("https://evil.test/api/v1/my/import/rss-feeds/id")]
  [InlineData("/api/v1/my/import/rss-feeds/wrong")]
  [InlineData("/api/v1/my/import/rss-feeds/id?x=1")]
  public async Task RejectsUntrustedStatusUrlWithoutReadingOrReposting(string url)
  {
    var service = new FakeService { Submission = Submission() with { StatusUrl = new Uri(url, UriKind.RelativeOrAbsolute) } };
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.NotNull(model.Error);
    Assert.False(model.CanRetry);
    await model.CreateAsync();
    Assert.Equal(1, service.Posts);
    Assert.Equal(0, service.Gets);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("bad")]
  [InlineData("ftp://example.test/feed")]
  [InlineData("https://u:p@example.test/feed")]
  public async Task InvalidInputDoesNotSubmit(string? input)
  {
    var service = new FakeService();
    using var model = Model(service);
    model.Input = input!;
    await model.CreateAsync();
    Assert.NotNull(model.Error);
    Assert.True(model.CanCreate);
    Assert.Equal(0, service.Posts);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CancellationAndAccountChangeFenceLateSubmission(bool accountChange)
  {
    var pending = new TaskCompletionSource<RssFeedImportSubmission>();
    var service = new FakeService { Post = () => pending.Task };
    var session = new Session();
    using var model = Model(service, session);
    model.Input = "https://example.test/feed.xml";
    var operation = model.CreateAsync();
    Assert.True(model.IsBusy);
    await model.CreateAsync();
    if (accountChange) session.Change(); else model.Cancel();
    pending.SetResult(Submission());
    await operation;
    Assert.Null(model.EntityId);
    Assert.False(model.IsBusy);
    Assert.Equal(0, service.Gets);
    Assert.Equal(1, service.Posts);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task AccountChangeFencesLateStatusSuccessAndFailure(bool failure)
  {
    var pending = new TaskCompletionSource<RssFeedImportStatus>();
    var service = new FakeService();
    service.Reads.Enqueue(() => pending.Task);
    var session = new Session();
    using var model = Model(service, session);
    model.Input = "https://example.test/feed.xml";
    var operation = model.CreateAsync();
    session.Change();
    if (failure) pending.SetException(new HttpRequestException());
    else pending.SetResult(Status("completed"));
    await operation;
    Assert.Null(model.EntityId);
    Assert.Null(model.Error);
    Assert.True(model.CanCreate);
    Assert.False(model.CanOpen);
    Assert.Equal(1, service.Posts);
  }

  [Fact]
  public async Task PollingStopsAtBudgetAndResumesWithoutPost()
  {
    var service = new FakeService { DefaultStatus = Status("pending") };
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.Equal(30, service.Gets);
    Assert.True(model.CanRetry);
    service.DefaultStatus = Status("completed");
    await model.RetryAsync();
    Assert.True(model.CanOpen);
    Assert.Equal(1, service.Posts);
  }

  [Fact]
  public async Task CompletedOutcomeCannotOpenAfterApiChangesOrDisposal()
  {
    var service = new FakeService();
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.True(model.CanOpen);
    service.ApiOrigin = new("https://other.test");
    Assert.False(model.CanOpen);
    service.ApiOrigin = new("https://api.test");
    Assert.True(model.CanOpen);
    model.Dispose();
    Assert.False(model.CanOpen);
  }

  [Fact]
  public async Task SameAccountRefreshDoesNotDiscardAcceptedOperation()
  {
    var accepted = new TaskCompletionSource<RssFeedImportSubmission>();
    var service = new FakeService { Post = () => accepted.Task };
    var session = new Session();
    using var model = Model(service, session);
    model.Input = "https://example.test/feed.xml";
    var operation = model.CreateAsync();
    await session.RefreshAsync();
    Assert.True(model.IsBusy);
    accepted.SetResult(Submission());
    await operation;
    Assert.True(model.CanOpen);
    await model.CreateAsync();
    Assert.Equal(1, service.Posts);
  }

  [Fact]
  public async Task AmbiguousPostFailureNeverAllowsDuplicateCreation()
  {
    var service = new FakeService { Post = () => throw new HttpRequestException() };
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.NotNull(model.Error);
    Assert.False(model.CanCreate);
    Assert.False(model.CanRetry);
    await model.CreateAsync();
    Assert.Equal(1, service.Posts);
  }

  [Fact]
  public async Task PendingTypedErrorKeepsPollingAndNeverBecomesTerminalFailure()
  {
    var service = new FakeService();
    var pending = Status("pending");
    service.Reads.Enqueue(() => Task.FromResult(pending with { Rows = [pending.Rows[0] with { ErrorCode = RssFeedImportErrorCode.ImportFailed }] }));
    service.Reads.Enqueue(() => throw new HttpRequestException());
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.Contains("Retrying", model.Status, StringComparison.Ordinal);
    Assert.True(model.CanRetry);
    await model.RetryAsync();
    Assert.True(model.CanOpen);
    Assert.Equal(1, service.Posts);
  }

  [Fact]
  public async Task WrongIdentityAndRegressingStatusAreRetryableReadFailures()
  {
    var service = new FakeService();
    service.Reads.Enqueue(() => Task.FromResult(Status("pending") with { Import = Status("pending").Import with { Id = "foreign" } }));
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.NotNull(model.Error);
    Assert.Null(model.EntityId);
    await model.RetryAsync();
    Assert.True(model.CanOpen);
    Assert.Equal(1, service.Posts);
  }

  [Fact]
  public async Task TerminalSummaryCannotMakePendingRowNavigable()
  {
    var completed = Status("completed");
    var service = new FakeService { DefaultStatus = completed with { Rows = [completed.Rows[0] with { Status = ImportResultStatus.Pending, TopicSlug = "source", ErrorCode = RssFeedImportErrorCode.ImportFailed }] } };
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.False(model.CanOpen);
    Assert.Null(model.Error);
    Assert.Contains("Retrying", model.Status, StringComparison.Ordinal);
    Assert.True(model.CanRetry);
    service.DefaultStatus = completed;
    await model.RetryAsync();
    Assert.True(model.CanOpen);
    Assert.Equal(1, service.Posts);
  }

  [Fact]
  public async Task ContradictoryTerminalSummaryCannotNavigate()
  {
    var completed = Status("completed");
    var service = new FakeService { DefaultStatus = completed with { Import = completed.Import with { FailedRows = 1, CompletedRows = 0 } } };
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.False(model.CanOpen);
    Assert.Null(model.EntityId);
    Assert.NotNull(model.Error);
  }

  [Fact]
  public async Task RegressingSummaryDoesNotReplaceLastSnapshot()
  {
    var service = new FakeService();
    var pending = Status("pending");
    service.Reads.Enqueue(() => Task.FromResult(pending with { Import = pending.Import with { PendingRows = 2 } }));
    using var model = Model(service);
    model.Input = "https://example.test/feed.xml";
    await model.CreateAsync();
    Assert.NotNull(model.Error);
    Assert.Null(model.EntityId);
    Assert.True(model.CanRetry);
  }

  [Fact]
  public async Task NewAccountOperationSuppressesOldSuccessAndOldFailure()
  {
    var old = new TaskCompletionSource<RssFeedImportSubmission>();
    var service = new FakeService { Post = () => old.Task };
    var session = new Session();
    using var model = Model(service, session);
    model.Input = "https://example.test/feed.xml";
    var first = model.CreateAsync();
    session.Change();
    service.Post = null;
    await model.CreateAsync();
    old.SetException(new HttpRequestException());
    await first;
    Assert.True(model.CanOpen);
    Assert.Null(model.Error);
    Assert.Equal(2, service.Posts);
    Assert.Equal(1, service.Gets);
  }

  internal static RssFeedImportStatus Status(string state) => JsonSerializer.Deserialize<RssFeedImportStatus>(
      ApiFixtureLoader.LoadResponse($"native.rss-feeds.create.status.{state}"), VouchaApiJson.Options)!;
  internal static RssFeedImportSubmission Submission() => JsonSerializer.Deserialize<RssFeedImportSubmission>(
      ApiFixtureLoader.LoadResponse("native.rss-feeds.create.accepted"), VouchaApiJson.Options)!;
  internal static SourceCreationViewModel Model(FakeService service, Session? session = null) =>
      new(service, session ?? new(), UiLocalization.English, (_, _) => Task.CompletedTask);

  internal sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = new(new User("member", "member"));
    public void Change() { Current = new(new User("other", "other")); SessionChanged?.Invoke(this, new(Current)); }
    public Task RefreshAsync(CancellationToken cancellationToken = default) { SessionChanged?.Invoke(this, new(Current)); return Task.CompletedTask; }
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  internal sealed class FakeService : ISourceCreationService
  {
    public Uri ApiOrigin { get; set; } = new("https://api.test");
    public RssFeedImportSubmission Submission { get; set; } = SourceCreationTests.Submission();
    public RssFeedImportStatus DefaultStatus { get; set; } = Status("completed");
    public Func<Task<RssFeedImportSubmission>>? Post { get; set; }
    public Queue<Func<Task<RssFeedImportStatus>>> Reads { get; } = new();
    public int Posts { get; private set; }
    public int Gets { get; private set; }
    public Task<RssFeedImportSubmission> CreateAsync(Uri url, CancellationToken token) { Posts++; return Post?.Invoke() ?? Task.FromResult(Submission); }
    public Task<RssFeedImportStatus> ReadAsync(string importId, CancellationToken token) { Gets++; Assert.Equal(Submission.Import.Id, importId); return Reads.Count > 0 ? Reads.Dequeue()() : Task.FromResult(DefaultStatus); }
    public Task<CreatedSourceDetail> ResolveAsync(string feedId, CancellationToken token) => Task.FromResult(new CreatedSourceDetail(feedId, "resolved-topic"));
  }
}
