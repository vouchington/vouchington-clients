using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class MemberAppealsSubmissionLifecycleTests
{
  [Fact]
  public async Task CancelKeepsSubmissionGatedAndRejectsItsLateCompletion()
  {
    var service = new CancellationIgnoringService();
    var viewModel = new MemberAppealsViewModel(
        service,
        new NavigationViewer(true, [], IdentityId: "user-1"),
        MemberAppealsRoute.Warnings);
    var page = new MemberAppealsPage(viewModel, new TokenProvider());
    await page.ReloadAsync();
    Find<Button>(page, "member-appeal-file-warning:warning-1").SendClicked();
    Find<Picker>(page, "member-appeal-reason").SelectedIndex = 4;
    Find<Editor>(page, "member-appeal-details").Text = "Draft details";

    Find<Button>(page, "member-appeal-submit").SendClicked();
    await service.SubmissionStarted.Task;
    Find<Button>(page, "member-appeal-cancel").SendClicked();
    await service.SubmissionCancellationObserved.Task;

    Find<Button>(page, "member-appeal-file-warning:warning-1").SendClicked();
    Assert.False(Find<Button>(page, "member-appeal-submit").IsEnabled);
    service.ReleaseSubmission();
    await WaitUntilAsync(() => Find<Button>(page, "member-appeal-submit").IsEnabled);

    Assert.NotNull(viewModel.ActiveTarget);
    Assert.Equal("Draft details", Find<Editor>(page, "member-appeal-details").Text);
    Assert.Equal(1, service.SubmissionCalls);
  }

  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(
          root.GetVisualTreeDescendants().OfType<T>(),
          item => item.AutomationId == id);

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

  private sealed class CancellationIgnoringService : IMemberAppealsService
  {
    private readonly TaskCompletionSource submissionRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource SubmissionStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource SubmissionCancellationObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int SubmissionCalls { get; private set; }

    public Task<ModerationAppealListResponse> FetchAppealsAsync(
        ModerationAppealStatus status,
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModerationAppealListResponse(
            [], new PageInfo(null, false, null)));

    public Task<MemberWarningNoticesResponse> FetchWarningsAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MemberWarningNoticesResponse(
            [new("warning-1", "case-1", "user-1", null, null, "Warning", null,
                DateTimeOffset.UtcNow)],
            new PageInfo(null, false, null)));

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

    public async Task<ModerationAppealSubmissionResponse> SubmitAsync(
        ModerationAppealSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
      SubmissionCalls++;
      using var registration = cancellationToken.Register(
          SubmissionCancellationObserved.SetResult);
      SubmissionStarted.SetResult();
      await submissionRelease.Task;
      return new ModerationAppealSubmissionResponse(Appeal(), false);
    }

    public void ReleaseSubmission() => submissionRelease.TrySetResult();

    private static ModerationAppeal Appeal() =>
        new(
            "appeal-1", "case-1", "user-1", "warning-1", null, null, null,
            null, "reason", ModerationAppealStatus.Pending, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
  }
}
