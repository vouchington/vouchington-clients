using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public async Task<bool> JoinAsync(CancellationToken cancellationToken = default)
  {
    if (!CanJoin || string.IsNullOrWhiteSpace(communityIdOrSlug))
    {
      return false;
    }

    return await MutateAndReloadAsync(() => service.JoinAsync(communityIdOrSlug, cancellationToken), cancellationToken).ConfigureAwait(true);
  }

  public async Task<bool> LeaveAsync(CancellationToken cancellationToken = default)
  {
    if (!CanLeave || string.IsNullOrWhiteSpace(communityIdOrSlug))
    {
      return false;
    }

    return await MutateAndReloadAsync(() => service.LeaveAsync(communityIdOrSlug, cancellationToken), cancellationToken).ConfigureAwait(true);
  }

  public async Task<bool> ArchiveAsync(CancellationToken cancellationToken = default)
  {
    if (!CanArchive || string.IsNullOrWhiteSpace(communityIdOrSlug))
    {
      return false;
    }

    return await MutateAndApplyAsync(() => service.ArchiveAsync(communityIdOrSlug, cancellationToken), cancellationToken).ConfigureAwait(true);
  }

  public async Task<bool> UnarchiveAsync(CancellationToken cancellationToken = default)
  {
    if (!CanUnarchive || string.IsNullOrWhiteSpace(communityIdOrSlug))
    {
      return false;
    }

    return await MutateAndApplyAsync(() => service.UnarchiveAsync(communityIdOrSlug, cancellationToken), cancellationToken).ConfigureAwait(true);
  }

  private async Task<bool> MutateAndReloadAsync(Func<Task> mutation, CancellationToken cancellationToken)
  {
    if (IsLoading)
    {
      return false;
    }

    BeginMutation();
    try
    {
      await mutation().ConfigureAwait(true);
      await LoadAsync(communityIdOrSlug, cancellationToken).ConfigureAwait(true);
      return !HasError;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
      return false;
    }
  }

  private async Task<bool> MutateAndApplyAsync(
      Func<Task<CommunityMutationResponse>> mutation,
      CancellationToken cancellationToken)
  {
    if (IsLoading)
    {
      return false;
    }

    BeginMutation();
    try
    {
      var response = await mutation().ConfigureAwait(true);
      Community = response.Community;
      CompleteLoad();
      return true;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
      return false;
    }
  }
}
