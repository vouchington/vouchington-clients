using Microsoft.Maui.Controls;
using System.Reflection;
using Voucha.Client.App;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed partial class MemberAppealsPageTests
{
  [Fact]
  public async Task RendersMemberNoticeFormAndTrackingLifecycle()
  {
    var service = new MemberService();
    var warningViewModel = new MemberAppealsViewModel(
        service,
        new NavigationViewer(true, [], IdentityId: "user-1"),
        MemberAppealsRoute.Warnings);
    var page = new MemberAppealsPage(warningViewModel, new TokenProvider());
    await page.ReloadAsync();

    Find<Button>(page, "member-appeal-file-warning:warning-1").SendClicked();

    Assert.NotNull(Find<Picker>(page, "member-appeal-reason"));
    Assert.Equal(3800, Find<Editor>(page, "member-appeal-details").MaxLength);
    Assert.NotNull(Find<Button>(page, "member-appeal-submit"));

    service.IncludeAppeal = true;
    service.Appeal = service.Appeal with
    {
      Status = ModerationAppealStatus.Resolved,
      ApprovedAt = DateTimeOffset.Parse("2026-07-01T01:00:00Z"),
      SentAt = DateTimeOffset.Parse("2026-07-01T02:00:00Z"),
      ResolvedAt = DateTimeOffset.Parse("2026-07-01T03:00:00Z"),
      ResolutionAction = ModerationAppealAction.Accept,
    };
    var tracking = new MemberAppealsPage(
        new MemberAppealsViewModel(
            service,
            new NavigationViewer(true, [], IdentityId: "user-1"),
            MemberAppealsRoute.Tracking),
        new TokenProvider());
    await tracking.ReloadAsync();
    Assert.NotNull(Find<Border>(tracking, "member-appeal-appeal-1"));
    Assert.NotNull(Find<Label>(tracking, "member-appeal-approved-appeal-1"));
    Assert.NotNull(Find<Label>(tracking, "member-appeal-sent-appeal-1"));
    Assert.NotNull(Find<Label>(tracking, "member-appeal-resolved-appeal-1"));
    Assert.NotNull(Find<Label>(tracking, "member-appeal-resolution-appeal-1"));
  }

  [Fact]
  public async Task RouteRendersOnlyRelevantSectionsAndPagination()
  {
    var service = new MemberService { WarningHasMore = true };
    var page = new MemberAppealsPage(
        new MemberAppealsViewModel(
            service,
            new NavigationViewer(true, [], IdentityId: "user-1"),
            MemberAppealsRoute.Warnings),
        new TokenProvider());

    await page.ReloadAsync();

    Assert.NotNull(Find<HybridPaginationControl>(page, "pagination-member-warnings"));
    Assert.DoesNotContain(Descendants<Label>(page),
        item => item.AutomationId == "member-appeals-tracking-heading");
    Assert.DoesNotContain(Descendants<HybridPaginationControl>(page),
        item => item.AutomationId == "pagination-member-bans");
  }

  [Fact]
  public async Task InitialStreamLoadingAndFailureRetryAreRenderedAndOperational()
  {
    var service = new MemberService();
    service.BlockWarnings();
    var page = Page(service, MemberAppealsRoute.Warnings);

    var load = page.ReloadAsync();
    await service.WarningStarted.Task;
    await WaitUntilAsync(() => Descendants<HybridPaginationControl>(page).Any(
        item => item.AutomationId == "pagination-member-warnings"));

    var loading = Find<HybridPaginationControl>(page, "pagination-member-warnings");
    Assert.True(loading.IsLoading);
    Assert.True(loading.HasMore);
    service.ReleaseWarnings();
    await load;

    service.WarningError = new HttpRequestException("warnings unavailable");
    await page.ReloadAsync();
    var retry = Find<HybridPaginationControl>(page, "pagination-member-warnings");
    Assert.True(retry.HasError);

    Find<Button>(page, "pagination-member-warnings-action").SendClicked();
    await WaitUntilAsync(() =>
        service.WarningCalls == 3 &&
        Descendants<Button>(page).Any(item =>
            item.AutomationId == "member-appeal-file-warning:warning-1"));

    Assert.DoesNotContain(Descendants<HybridPaginationControl>(page),
        item => item.AutomationId == "pagination-member-warnings" && item.HasError);
    Assert.NotNull(Find<Button>(page, "member-appeal-file-warning:warning-1"));
  }

  [Fact]
  public async Task DisappearingCancelsAnInitialLoadWithoutFaultingIt()
  {
    var service = new MemberService();
    service.BlockWarnings();
    var page = Page(service, MemberAppealsRoute.Warnings);
    var load = page.ReloadAsync();
    await service.WarningStarted.Task;

    typeof(MemberAppealsPage)
        .GetMethod("OnDisappearing", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(page, null);

    await load;
    Assert.True(load.IsCompletedSuccessfully);
  }

  [Fact]
  public async Task SameRouteReusesPageAndEnsureLoadedDoesNotDoubleLoad()
  {
    var service = new MemberService();
    service.BlockWarnings();
    var viewer = new MutableNavigationViewerProvider();
    viewer.SetViewer(new NavigationViewer(true, [], IdentityId: "user-1"));
    using var controller = new UiLocaleController(new Languages());
    var factory = new MemberAppealsPageFactory(
        service, viewer, new MemberAppealDraftStore(), new TokenProvider(), controller);
    var existing = factory.Create(MemberAppealsRoute.Warnings);

    var reused = factory.ReuseOrCreate(existing, MemberAppealsRoute.Warnings);
    var first = reused.EnsureLoadedAsync();
    await service.WarningStarted.Task;
    var second = reused.EnsureLoadedAsync();

    Assert.Same(existing, reused);
    Assert.Same(first, second);
    Assert.Equal(1, service.WarningCalls);
    Assert.NotSame(
        existing,
        factory.ReuseOrCreate(existing, MemberAppealsRoute.Bans));
    service.ReleaseWarnings();
    await Task.WhenAll(first, second);
  }

  [Fact]
  public async Task TrackingRefreshesPendingToDeniedResolutionAndThenEmpty()
  {
    var service = new MemberService { IncludeAppeal = true };
    var page = Page(service, MemberAppealsRoute.Tracking);

    await page.ReloadAsync();
    Assert.NotNull(Find<Border>(page, "member-appeal-appeal-1"));
    Assert.NotNull(Find<Label>(page, "member-appeals-status-pending"));

    service.Appeal = service.Appeal with
    {
      Status = ModerationAppealStatus.Resolved,
      ResolvedAt = DateTimeOffset.Parse("2026-07-01T03:00:00Z"),
      ResolutionAction = ModerationAppealAction.Deny,
    };
    await page.ReloadAsync();
    Assert.NotNull(Find<Label>(page, "member-appeals-status-resolved"));
    Assert.Contains(
        UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsDeny),
        Find<Label>(page, "member-appeal-resolution-appeal-1").Text,
        StringComparison.Ordinal);

    service.IncludeAppeal = false;
    await page.ReloadAsync();
    Assert.DoesNotContain(Descendants<Border>(page),
        item => item.AutomationId == "member-appeal-appeal-1");
    Assert.NotNull(Find<Label>(page, "member-appeals-tracking-empty"));
  }

  [Fact]
  public async Task PendingTrackingCardUsesMemberSafeTargetContextAndCreationDate()
  {
    var createdAt = DateTimeOffset.Parse("2026-07-01T01:02:00Z");
    var service = new MemberService { IncludeAppeal = true };
    service.Appeal = service.Appeal with
    {
      AppealReason = null,
      PublicResponse = null,
      CreatedAt = createdAt,
      TargetContext = new ModerationAppealPostRemovalContext(
          "post-1",
          "A removed review",
          ModerationAppealPostRemovalKind.Platform,
          null,
          "Policy reason visible to the member",
          createdAt),
    };
    var page = Page(service, MemberAppealsRoute.Tracking);

    await page.ReloadAsync();

    Assert.Equal(
        "A removed review",
        Find<Label>(page, "member-appeal-target-appeal-1").Text);
    Assert.Contains(
        UiCopy.FormatDateTime(createdAt),
        Find<Label>(page, "member-appeal-created-appeal-1").Text,
        StringComparison.Ordinal);
    Assert.DoesNotContain(
        Descendants<Label>(page),
        label => string.Equals(label.Text, service.Appeal.Id, StringComparison.Ordinal));
  }

  [Fact]
  public async Task TrackingPendingFailureHidesFilingUntilRenderedRetryRecovers()
  {
    var service = new MemberService();
    service.AppealErrors[ModerationAppealStatus.Pending] =
        new HttpRequestException("pending unavailable");
    var page = Page(service, MemberAppealsRoute.Tracking);

    await page.ReloadAsync();

    Assert.NotNull(Find<Label>(page, "member-appeals-error"));
    Assert.DoesNotContain(Descendants<Button>(page),
        item => item.AutomationId == "member-appeal-file-warning:warning-1");

    Find<Button>(page, "member-appeals-retry").SendClicked();
    await WaitUntilAsync(() => Descendants<Button>(page).Any(
        item => item.AutomationId == "member-appeal-file-warning:warning-1"));

    Assert.DoesNotContain(Descendants<Label>(page),
        item => item.AutomationId == "member-appeals-error");
    Assert.Equal(2, service.AppealCalls[ModerationAppealStatus.Pending]);
  }

  [Fact]
  public async Task TurnstileAcquisitionIsLockedAcrossDoubleTap()
  {
    var service = new MemberService();
    var tokens = new BlockingTokenProvider();
    var page = await FormPageAsync(service, tokens);
    Find<Button>(page, "member-appeal-submit").SendClicked();
    await tokens.Started.Task;
    Find<Button>(page, "member-appeal-submit").SendClicked();

    Assert.Equal(1, tokens.Calls);
    tokens.Release("fresh-token");
    await WaitUntilAsync(() => service.SubmissionCalls == 1);
    Assert.Equal("fresh-token", service.LastToken);
  }

  [Fact]
  public async Task TurnstileFailureKeepsDraftAndAllowsFreshRetry()
  {
    var service = new MemberService();
    var tokens = new RetryTokenProvider();
    var page = await FormPageAsync(service, tokens);

    Find<Button>(page, "member-appeal-submit").SendClicked();
    await WaitUntilAsync(() => Descendants<Label>(page).Any(
        item => item.AutomationId == "member-appeal-submission-message"));
    Assert.Equal("Draft details", Find<Editor>(page, "member-appeal-details").Text);

    Find<Button>(page, "member-appeal-submit").SendClicked();
    await WaitUntilAsync(() => service.SubmissionCalls == 1);
    Assert.Equal(2, tokens.Calls);
    Assert.Equal("retry-token", service.LastToken);
  }

  [Fact]
  public void SignedOutMemberRouteRendersSignInStateWithoutStaffControls()
  {
    var page = new MemberAppealsPage(
        new MemberAppealsViewModel(
            new MemberService(),
            NavigationViewer.Anonymous,
            MemberAppealsRoute.Tracking),
        new TokenProvider());

    Assert.NotNull(Find<Label>(page, "member-appeals-sign-in"));
    Assert.Empty(page.GetVisualTreeDescendants().OfType<Picker>());
  }

  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(Descendants<T>(root), item => item.AutomationId == id);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private static async Task<MemberAppealsPage> FormPageAsync(
      MemberService service,
      ITurnstileTokenProvider tokens)
  {
    var page = new MemberAppealsPage(
        new MemberAppealsViewModel(
            service,
            new NavigationViewer(true, [], IdentityId: "user-1"),
            MemberAppealsRoute.Warnings),
        tokens);
    await page.ReloadAsync();
    Find<Button>(page, "member-appeal-file-warning:warning-1").SendClicked();
    Find<Picker>(page, "member-appeal-reason").SelectedIndex = 4;
    Find<Editor>(page, "member-appeal-details").Text = "Draft details";
    return page;
  }

  private static MemberAppealsPage Page(
      MemberService service,
      MemberAppealsRoute route) =>
      new(
          new MemberAppealsViewModel(
              service,
              new NavigationViewer(true, [], IdentityId: "user-1"),
              route),
          new TokenProvider());

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++)
      await Task.Delay(10);
    Assert.True(condition());
  }

  private sealed class TokenProvider : ITurnstileTokenProvider
  {
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult("token");
  }

  private sealed class Languages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }

  private sealed class BlockingTokenProvider : ITurnstileTokenProvider
  {
    private readonly TaskCompletionSource<string> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls { get; private set; }

    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
      Calls++;
      Started.TrySetResult();
      return completion.Task.WaitAsync(cancellationToken);
    }

    public void Release(string token) => completion.TrySetResult(token);
  }

  private sealed class RetryTokenProvider : ITurnstileTokenProvider
  {
    public int Calls { get; private set; }

    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
      Calls++;
      return Calls == 1
          ? Task.FromException<string>(new HttpRequestException("challenge unavailable"))
          : Task.FromResult("retry-token");
    }
  }

  private sealed class MemberService : IMemberAppealsService
  {
    private TaskCompletionSource<MemberWarningNoticesResponse>? pendingWarnings;
    public bool IncludeAppeal { get; set; }
    public bool WarningHasMore { get; set; }
    public Exception? WarningError { get; set; }
    public Dictionary<ModerationAppealStatus, Exception> AppealErrors { get; } = [];
    public Dictionary<ModerationAppealStatus, int> AppealCalls { get; } =
        Enum.GetValues<ModerationAppealStatus>().ToDictionary(status => status, _ => 0);
    public TaskCompletionSource WarningStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int WarningCalls { get; private set; }
    public int SubmissionCalls { get; private set; }
    public string? LastToken { get; private set; }
    public ModerationAppeal Appeal { get; set; } = CreateAppeal();

    public Task<ModerationAppealListResponse> FetchAppealsAsync(
        ModerationAppealStatus status,
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      AppealCalls[status]++;
      if (AppealErrors.Remove(status, out var error))
        return Task.FromException<ModerationAppealListResponse>(error);
      var includesAppeal = IncludeAppeal && status == Appeal.Status && after is null;
      return Task.FromResult(new ModerationAppealListResponse(
            includesAppeal ? [Appeal] : [],
            new PageInfo(
                includesAppeal ? "appeal-next" : null,
                includesAppeal,
                null)));
    }

    public async Task<MemberWarningNoticesResponse> FetchWarningsAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      WarningCalls++;
      if (WarningError is { } error)
      {
        WarningError = null;
        throw error;
      }
      if (pendingWarnings is { } pending)
      {
        WarningStarted.TrySetResult();
        return await pending.Task.WaitAsync(cancellationToken);
      }
      return new MemberWarningNoticesResponse(
            [new("warning-1", "case-1", "user-1", null, null,
                "A moderation warning", null, DateTimeOffset.UtcNow)],
            new PageInfo(
                WarningHasMore ? "warning-next" : null,
                WarningHasMore,
                null));
    }

    public Task<PersonalCommunityBansResponse> FetchBansAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PersonalCommunityBansResponse([], new PageInfo(null, false, null)));

    public Task<PersonalRemovedPostsResponse> FetchRemovedPostsAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PersonalRemovedPostsResponse([], new PageInfo(null, false, null)));

    public Task<MyIdentityResponse> FetchIdentityAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MyIdentityResponse(new User("user-1", "member")));

    public Task<ModerationAppealSubmissionResponse> SubmitAsync(
        ModerationAppealSubmissionRequest request,
      CancellationToken cancellationToken = default)
    {
      SubmissionCalls++;
      LastToken = request.TurnstileToken;
      return Task.FromResult(new ModerationAppealSubmissionResponse(Appeal, false));
    }

    public void BlockWarnings()
    {
      pendingWarnings = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void ReleaseWarnings()
    {
      pendingWarnings?.TrySetResult(new MemberWarningNoticesResponse(
          [new("warning-1", "case-1", "user-1", null, null,
              "A moderation warning", null, DateTimeOffset.UtcNow)],
          new PageInfo(null, false, null)));
      pendingWarnings = null;
    }

    private static ModerationAppeal CreateAppeal() =>
        new(
            "appeal-1", "case-1", "user-1", "warning-1", null, null, null, null,
            "reason", ModerationAppealStatus.Pending, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
  }
}
