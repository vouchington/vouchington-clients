using Voucha.Client.Core.Api;
using Voucha.Client.Core.Friends;

namespace Voucha.Client.Core.Tests.Friends;

public sealed partial class FriendsViewModelTests
{
  private static UserFollowingResponse CreateFollowingResponse(params User[] users) =>
      new(users, new PageInfo(null, false, null));

  private static UserFollowingResponse CreateFollowingResponse(PageInfo pageInfo, params User[] users) =>
      new(users, pageInfo);

  private static UserFollowersResponse CreateFollowersResponse(params User[] users) =>
      new(users, new PageInfo(null, false, null));

  private static UserFollowersResponse CreateFollowersResponse(PageInfo pageInfo, params User[] users) =>
      new(users, pageInfo);

  private sealed class RecordingFriendsService : IFriendsService
  {
    public RecordingFriendsService(
        UserFollowingResponse? following = null,
        UserFollowersResponse? followers = null)
    {
      FollowingResponse = following ?? CreateFollowingResponse();
      FollowersResponse = followers ?? CreateFollowersResponse();
    }

    public UserFollowingResponse FollowingResponse { get; set; }

    public UserFollowersResponse FollowersResponse { get; set; }

    public Queue<UserFollowingResponse> FollowingResponses { get; } = [];

    public Queue<UserFollowersResponse> FollowersResponses { get; } = [];

    public bool FailMutations { get; set; }

    public bool FailFollowingFetch { get; set; }

    public bool FailFollowersFetch { get; set; }

    public IReadOnlyDictionary<string, TaskCompletionSource>? FollowCompletions { get; init; }

    public IReadOnlyDictionary<string, Queue<TaskCompletionSource>>? FollowCompletionQueues { get; init; }

    public TaskCompletionSource<UserFollowingResponse>? PendingFollowingFetch { get; set; }

    public TaskCompletionSource<UserFollowersResponse>? PendingFollowersFetch { get; set; }

    public int FollowingFetchCount { get; private set; }

    public List<string> FollowingFetchUserIds { get; } = [];

    public List<string?> FollowingFetchAfterValues { get; } = [];

    public List<string?> FollowersFetchAfterValues { get; } = [];

    public List<(string UserId, bool Following)> FollowCalls { get; } = [];

    public Task<UserFollowingResponse> FetchFollowingAsync(
        string userId,
        string? after = null,
        CancellationToken cancellationToken = default)
    {
      FollowingFetchCount++;
      FollowingFetchUserIds.Add(userId);
      FollowingFetchAfterValues.Add(after);
      if (FailFollowingFetch)
      {
        throw new InvalidOperationException("Following fetch failed.");
      }

      if (PendingFollowingFetch is not null)
      {
        var pendingFollowingFetch = PendingFollowingFetch;
        PendingFollowingFetch = null;
        return pendingFollowingFetch.Task;
      }

      if (FollowingResponses.TryDequeue(out var followingResponse))
      {
        return Task.FromResult(followingResponse);
      }

      return Task.FromResult(FollowingResponse);
    }

    public Task<UserFollowersResponse> FetchFollowersAsync(
        string userId,
        string? query = null,
        string? after = null,
        CancellationToken cancellationToken = default)
    {
      FollowersFetchAfterValues.Add(after);
      if (FailFollowersFetch)
      {
        throw new InvalidOperationException("Followers fetch failed.");
      }

      if (PendingFollowersFetch is not null)
      {
        var pendingFollowersFetch = PendingFollowersFetch;
        PendingFollowersFetch = null;
        return pendingFollowersFetch.Task;
      }

      if (FollowersResponses.TryDequeue(out var followersResponse))
      {
        return Task.FromResult(followersResponse);
      }

      return Task.FromResult(FollowersResponse);
    }

    public Task SetFollowAsync(
        string userId,
        bool following,
        CancellationToken cancellationToken = default)
    {
      FollowCalls.Add((userId, following));
      if (FollowCompletionQueues?.TryGetValue(userId, out var completions) == true && completions.TryDequeue(out var queuedCompletion))
      {
        return queuedCompletion.Task;
      }

      if (FollowCompletions?.TryGetValue(userId, out var completion) == true)
      {
        return completion.Task;
      }

      if (FailMutations)
      {
        throw new InvalidOperationException("Mutation failed.");
      }

      return Task.CompletedTask;
    }
  }
}
