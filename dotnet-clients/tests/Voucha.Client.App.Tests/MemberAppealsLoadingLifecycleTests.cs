using System.Reflection;
using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class MemberAppealsLoadingLifecycleTests
{
  [Fact]
  public async Task ReappearanceWaitsForCanceledReloadBeforeStartingReplacement()
  {
    var service = new SlowCancellationService();
    var page = new MemberAppealsPage(
        new MemberAppealsViewModel(
            service,
            new NavigationViewer(true, [], IdentityId: "user-1"),
            MemberAppealsRoute.Warnings),
        new TokenProvider());

    var first = page.ReloadAsync();
    await service.FirstWarningLoadStarted.Task;
    typeof(MemberAppealsPage)
        .GetMethod("OnDisappearing", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(page, null);
    await service.FirstWarningLoadCanceled.Task;

    var replacement = page.EnsureLoadedAsync();
    await Task.Yield();
    Assert.Equal(1, service.WarningCalls);
    service.ReleaseCanceledLoad();
    await Task.WhenAll(first, replacement);

    Assert.Equal(2, service.WarningCalls);
    Assert.Contains(
        page.GetVisualTreeDescendants().OfType<Button>(),
        button => button.AutomationId == "member-appeal-file-warning:warning-1");
  }

  private sealed class TokenProvider : ITurnstileTokenProvider
  {
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult("token");
  }

  private sealed class SlowCancellationService : IMemberAppealsService
  {
    private readonly TaskCompletionSource canceledLoadRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int WarningCalls { get; private set; }
    public TaskCompletionSource FirstWarningLoadStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource FirstWarningLoadCanceled { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ModerationAppealListResponse> FetchAppealsAsync(
        ModerationAppealStatus status,
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModerationAppealListResponse(
            [], new PageInfo(null, false, null)));

    public async Task<MemberWarningNoticesResponse> FetchWarningsAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      WarningCalls++;
      if (WarningCalls == 1)
      {
        FirstWarningLoadStarted.SetResult();
        try
        {
          await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          FirstWarningLoadCanceled.SetResult();
          await canceledLoadRelease.Task;
          throw;
        }
      }
      return new MemberWarningNoticesResponse(
          [new("warning-1", "case-1", "user-1", null, null, "Warning", null,
              DateTimeOffset.UtcNow)],
          new PageInfo(null, false, null));
    }

    public Task<PersonalCommunityBansResponse> FetchBansAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PersonalCommunityBansResponse(
            [], new PageInfo(null, false, null)));

    public Task<PersonalRemovedPostsResponse> FetchRemovedPostsAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PersonalRemovedPostsResponse(
            [], new PageInfo(null, false, null)));

    public Task<MyIdentityResponse> FetchIdentityAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MyIdentityResponse(new User("user-1", "member")));

    public Task<ModerationAppealSubmissionResponse> SubmitAsync(
        ModerationAppealSubmissionRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void ReleaseCanceledLoad() => canceledLoadRelease.TrySetResult();
  }
}
