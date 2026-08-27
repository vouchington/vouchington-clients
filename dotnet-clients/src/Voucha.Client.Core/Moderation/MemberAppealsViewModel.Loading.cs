using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class MemberAppealsViewModel
{
  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      ReloadAsync(cancellationToken);

  public async Task ReloadAsync(CancellationToken cancellationToken = default)
  {
    if (!IsSignedIn) return;
    var statuses = Route == MemberAppealsRoute.Tracking
        ? Enum.GetValues<ModerationAppealStatus>()
        : [ModerationAppealStatus.Pending];
    var tasks = statuses
        .Select(status => LoadAppealsAsync(status, null, replace: true, cancellationToken))
        .ToList();
    if (Route is MemberAppealsRoute.Tracking or MemberAppealsRoute.Warnings)
      tasks.Add(LoadWarningsAsync(null, replace: true, cancellationToken));
    if (Route is MemberAppealsRoute.Tracking or MemberAppealsRoute.Bans)
      tasks.Add(LoadBansAsync(null, replace: true, cancellationToken));
    if (Route is MemberAppealsRoute.Tracking or MemberAppealsRoute.RemovedPosts)
      tasks.Add(LoadRemovedPostsAsync(null, replace: true, cancellationToken));
    if (Route is MemberAppealsRoute.Tracking or MemberAppealsRoute.Suspension)
      tasks.Add(LoadIdentityAsync(cancellationToken));
    await Task.WhenAll(tasks).ConfigureAwait(true);
    var pending = appealStates[ModerationAppealStatus.Pending];
    if (pending.HasSuccessfulLoad && pending.HasMore)
      _ = await ReconcilePendingAppealsAsync(cancellationToken).ConfigureAwait(true);
  }

  private async Task LoadAppealsAsync(
      ModerationAppealStatus status,
      string? after,
      bool replace,
      CancellationToken cancellationToken)
  {
    var state = appealStates[status];
    if (!BeginLoad(state)) return;
    try
    {
      var response = await service.FetchAppealsAsync(
          status, after, cancellationToken: cancellationToken).ConfigureAwait(true);
      Complete(state, response.Appeals, response.PageInfo, item => item.Id, replace);
    }
    catch (OperationCanceledException)
    {
      CancelLoad(state);
      throw;
    }
    catch (HttpRequestException)
    {
      FailLoad(state, after, replace);
    }
    OnPropertyChanged(nameof(Appeals));
    OnTargetsChanged();
  }

  private async Task LoadWarningsAsync(
      string? after,
      bool replace,
      CancellationToken cancellationToken)
  {
    if (!BeginLoad(warnings)) return;
    try
    {
      var response = await service.FetchWarningsAsync(
          after, cancellationToken: cancellationToken).ConfigureAwait(true);
      Complete(warnings, response.Warnings, response.PageInfo, item => item.Id, replace);
    }
    catch (OperationCanceledException)
    {
      CancelLoad(warnings);
      throw;
    }
    catch (HttpRequestException)
    {
      FailLoad(warnings, after, replace);
    }
    OnPropertyChanged(nameof(WarningTargets));
    OnTargetsChanged();
  }

  private async Task LoadBansAsync(
      string? after,
      bool replace,
      CancellationToken cancellationToken)
  {
    if (!BeginLoad(bans)) return;
    try
    {
      var response = await service.FetchBansAsync(
          after, cancellationToken: cancellationToken).ConfigureAwait(true);
      Complete(bans, response.Bans, response.PageInfo, item => item.Id, replace);
    }
    catch (OperationCanceledException)
    {
      CancelLoad(bans);
      throw;
    }
    catch (HttpRequestException)
    {
      FailLoad(bans, after, replace);
    }
    OnPropertyChanged(nameof(BanTargets));
    OnTargetsChanged();
  }

  private async Task LoadRemovedPostsAsync(
      string? after,
      bool replace,
      CancellationToken cancellationToken)
  {
    if (!BeginLoad(removals)) return;
    try
    {
      var response = await service.FetchRemovedPostsAsync(
          after, cancellationToken: cancellationToken).ConfigureAwait(true);
      Complete(removals, response.RemovedPosts, response.PageInfo,
          item => $"{item.PostRemovalKind}:{item.PostId}", replace);
    }
    catch (OperationCanceledException)
    {
      CancelLoad(removals);
      throw;
    }
    catch (HttpRequestException)
    {
      FailLoad(removals, after, replace);
    }
    OnPropertyChanged(nameof(RemovedPostTargets));
    OnTargetsChanged();
  }

  private async Task LoadIdentityAsync(CancellationToken cancellationToken)
  {
    if (identityIsLoading) return;
    identityIsLoading = true;
    identityHasError = false;
    OnLoadStateChanged();
    try
    {
      var response = await service.FetchIdentityAsync(cancellationToken).ConfigureAwait(true);
      SuspensionTarget = response.Identity.SuspendedAt is { } suspendedAt
          ? MemberAppealTarget.Suspension(suspendedAt)
          : null;
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (HttpRequestException)
    {
      identityHasError = true;
    }
    finally
    {
      identityIsLoading = false;
      OnTargetsChanged();
    }
  }

}
