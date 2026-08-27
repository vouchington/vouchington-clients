using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;

namespace Voucha.Client.Core.Tests.Communities;

internal static class CommunityDetailViewModelCoverageFixtures
{
  public static void SeedLoadResponseSet(
      ScriptedCommunitiesService service,
      params CommunityResponse[] detailResponses)
  {
    foreach (var detail in detailResponses)
    {
      service.DetailResponses.Enqueue(detail);
      service.MembersResponses.Enqueue(CreateMembersResponse());
      service.PostsResponses.Enqueue(CreatePostsResponse());
      service.CountsResponses.Enqueue(new CommunityListItemCountsResponse(1, 2, 3, 4, 5));
    }
  }

  public static CommunityResponse CreateDetailResponse(
      bool hasMembership,
      bool hasPendingApplication,
      bool archived = false,
      string membershipRole = "member",
      string visibility = "public")
  {
    var archivedAt = archived ? "\"2026-07-01T00:00:00Z\"" : "null";
    var membership = hasMembership
        ? $$"""
          {
            "id": "membership-1",
            "community_id": "community-1",
            "user_id": "user-1",
            "role": "{{membershipRole}}"
          }
          """
        : "null";

    var json = $$"""
        {
          "community": {
            "id": "community-1",
            "name": "Community",
            "slug": "community-1",
            "markdown": "About",
            "visibility": "{{visibility}}",
            "member_roster_visibility": "members",
            "list_type": null,
            "member_invites_allowed_at": null,
            "post_approval_required_at": null,
            "allow_review_posts": true,
            "allow_data_point_posts": true,
            "created_by_id": "user-1",
            "created_at": "2026-07-01T00:00:00Z",
            "updated_at": "2026-07-01T00:00:00Z",
            "archived_at": {{archivedAt}}
          },
          "user": {
            "id": "user-1",
            "username": "owner"
          },
          "community_metrics": {
            "id": "community-1",
            "member_count": 2,
            "post_count": 1
          },
          "membership": {{membership}},
          "has_pending_application": {{hasPendingApplication.ToString().ToLowerInvariant()}}
        }
        """;

    return JsonSerializer.Deserialize<CommunityResponse>(json, VouchaApiJson.Options)
        ?? throw new InvalidOperationException("Community response JSON did not deserialize.");
  }

  private static CommunityMembersResponse CreateMembersResponse()
  {
    const string Json = """
        {
          "results": [
            { "id": "member-1" },
            { "id": "missing-member" }
          ],
          "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
          "community_members": {
            "member-1": {
              "id": "member-1",
              "community_id": "community-1",
              "user_id": "user-1",
              "role": "moderator",
              "created_at": "2026-07-01T00:00:00Z"
            }
          },
          "users": {}
        }
        """;

    return JsonSerializer.Deserialize<CommunityMembersResponse>(Json, VouchaApiJson.Options)
        ?? throw new InvalidOperationException("Community members JSON did not deserialize.");
  }

  private static CommunityPostsResponse CreatePostsResponse()
  {
    const string Json = """
        {
          "results": [
            { "id": "post-1" },
            { "id": "missing-post" }
          ],
          "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
          "posts": {
            "post-1": {
              "id": "post-1",
              "post_type": null,
              "title": null,
              "markdown": "Body",
              "created_by_id": "user-1",
              "slug": "fallback-title"
            }
          },
          "posts_metrics": {},
          "communities": {}
        }
        """;

    return JsonSerializer.Deserialize<CommunityPostsResponse>(Json, VouchaApiJson.Options)
        ?? throw new InvalidOperationException("Community posts JSON did not deserialize.");
  }
}

internal sealed partial class ScriptedCommunitiesService : ICommunitiesService
{
  public Queue<CommunityResponse> DetailResponses { get; } = [];

  public Queue<CommunityMembersResponse> MembersResponses { get; } = [];

  public Queue<CommunityPostsResponse> PostsResponses { get; } = [];

  public Queue<CommunityListItemCountsResponse> CountsResponses { get; } = [];

  public Queue<CommunitySavedRepliesResponse> SavedRepliesResponses { get; } = [];

  public Queue<CommunityAutomodActionsResponse> AutomodRecentActionsResponses { get; } = [];

  public Exception? DetailFailure { get; init; }

  public Exception? MembersFailure { get; init; }

  public Exception? PostsFailure { get; init; }

  public Exception? CountsFailure { get; init; }

  public Exception? ModerationTransparencyFailure { get; set; }

  public Exception? ArchiveFailure { get; init; }

  public Exception? UnarchiveFailure { get; init; }

  public Exception? JoinFailure { get; init; }

  public Exception? LeaveFailure { get; init; }

  public CommunitySearchResponse? SearchResponse { get; init; }

  public Exception? SearchFailure { get; init; }

  public Exception? CreateFailure { get; init; }

  public Exception? ApplyFailure { get; init; }

  public Exception? SendInviteFailure { get; init; }

  public Exception? RedeemInviteFailure { get; init; }

  public List<(string Method, string IdOrSlug)> MutationCalls { get; } = [];

  public Task<CommunityResponse> FetchDetailAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      DetailFailure is not null
          ? Task.FromException<CommunityResponse>(DetailFailure)
          : Task.FromResult(DetailResponses.Dequeue());

  public Task<CommunityMembersResponse> FetchMembersAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      MembersFailure is not null
          ? Task.FromException<CommunityMembersResponse>(MembersFailure)
          : Task.FromResult(MembersResponses.Dequeue());

  public Task<CommunityPostsResponse> FetchPostsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      PostsFailure is not null
          ? Task.FromException<CommunityPostsResponse>(PostsFailure)
          : Task.FromResult(PostsResponses.Dequeue());

  public Task<CommunityListItemCountsResponse> FetchListItemCountsAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      CountsFailure is not null
          ? Task.FromException<CommunityListItemCountsResponse>(CountsFailure)
          : Task.FromResult(CountsResponses.Dequeue());

  public Task<CommunityMutationResponse> ArchiveAsync(string idOrSlug, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("archive", idOrSlug));
    return ArchiveFailure is not null
        ? Task.FromException<CommunityMutationResponse>(ArchiveFailure)
        : Task.FromResult(new CommunityMutationResponse(CommunityDetailViewModelCoverageFixtures.CreateDetailResponse(
            hasMembership: true,
            hasPendingApplication: false,
            archived: true).Community));
  }

  public Task<CommunityMutationResponse> UnarchiveAsync(string idOrSlug, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("unarchive", idOrSlug));
    return UnarchiveFailure is not null
        ? Task.FromException<CommunityMutationResponse>(UnarchiveFailure)
        : Task.FromResult(new CommunityMutationResponse(CommunityDetailViewModelCoverageFixtures.CreateDetailResponse(
            hasMembership: true,
            hasPendingApplication: false).Community));
  }

  public Task JoinAsync(string idOrSlug, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("join", idOrSlug));
    return JoinFailure is not null ? Task.FromException(JoinFailure) : Task.CompletedTask;
  }

  public Task LeaveAsync(string idOrSlug, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("leave", idOrSlug));
    return LeaveFailure is not null ? Task.FromException(LeaveFailure) : Task.CompletedTask;
  }

  public Task<CommunitySearchResponse> SearchAsync(string query, int limit = 25, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(($"search:{query}:{limit}", ""));
    return SearchFailure is not null
        ? Task.FromException<CommunitySearchResponse>(SearchFailure)
        : Task.FromResult(SearchResponse ?? throw new NotSupportedException());
  }

  public Task<CommunityMutationResponse> CreateAsync(CreateCommunityRequest request, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("create", request.Slug ?? request.Name));
    return CreateFailure is not null
        ? Task.FromException<CommunityMutationResponse>(CreateFailure)
        : Task.FromResult(new CommunityMutationResponse(CommunityDetailViewModelCoverageFixtures.CreateDetailResponse(
            hasMembership: true,
            hasPendingApplication: false).Community));
  }

  public Task<CommunitySavedRepliesResponse> FetchSavedRepliesAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      Task.FromResult(SavedRepliesResponses.Count > 0 ? SavedRepliesResponses.Dequeue() : JsonSerializer.Deserialize<CommunitySavedRepliesResponse>("""
          {
            "results": [{ "id": "saved-reply-1" }],
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null },
            "community_saved_replies": {
              "saved-reply-1": {
                "id": "saved-reply-1",
                "community_id": "community-1",
                "title": "Greeting",
                "body": "Thanks for writing in.",
                "created_by_id": "user-1",
                "order_index": 0,
                "updated_at": "2026-07-01T00:00:00Z",
                "created_at": "2026-07-01T00:00:00Z"
              }
            }
          }
          """, VouchaApiJson.Options)!);

  public Task<CommunitySavedReply> CreateSavedReplyAsync(
      string idOrSlug,
      CreateCommunitySavedReplyRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(JsonSerializer.Deserialize<CommunitySavedReply>("""
          {
            "id": "saved-reply-1",
            "community_id": "community-1",
            "title": "Greeting",
            "body": "Thanks for writing in.",
            "created_by_id": "user-1",
            "order_index": 0,
            "updated_at": "2026-01-01T00:00:00Z",
            "created_at": "2026-01-01T00:00:00Z"
          }
          """, VouchaApiJson.Options)!);

  public Task DeleteSavedReplyAsync(string idOrSlug, string replyId, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("delete-saved-reply", replyId));
    return Task.CompletedTask;
  }

  public Task<CommunityAutomodActionsResponse> FetchAutomodRecentActionsAsync(
      string idOrSlug,
      string? after = null,
      int? limit = 25,
      string? window = null,
      string? source = null,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(AutomodRecentActionsResponses.Count > 0 ? AutomodRecentActionsResponses.Dequeue() : JsonSerializer.Deserialize<CommunityAutomodActionsResponse>("""
          {
            "automod_actions": [
              {
                "source_key": "agent_moderation:post-1",
                "source_type": "agent_moderation",
                "post_id": "post-1",
                "community_id": "community-1",
                "agent_moderation_id": "agent-moderation-1",
                "moderator_slug": "self-promotion",
                "title": "Fixture post",
                "markdown_preview": "Hello from the community.",
                "post_type": "discussion",
                "post_href": "/discussion/fixture-post",
                "created_at": "2026-01-01T00:00:00Z",
                "action_at": "2026-01-01T00:00:00Z",
                "confidence_score": 0.82,
                "flagged": true,
                "reason": "Low quality",
                "categories": ["quality"],
                "model_output": { "confidence_score": 0.82, "flagged": true, "reason": "Low quality" },
                "current_state": "rejected",
                "feedback_label": null
              }
            ],
            "stats": { "total_count": 1, "false_positive_count": 0, "false_positive_rate": 0 },
            "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
          }
          """, VouchaApiJson.Options)!);

  public Task<CommunityMutationResponse> UpdatePostTypeSettingsAsync(
      string idOrSlug,
      UpdateCommunityPostTypeSettingsRequest request,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("post-type-settings", idOrSlug));
    return Task.FromResult(new CommunityMutationResponse(CommunityDetailViewModelCoverageFixtures.CreateDetailResponse(
        hasMembership: true,
        hasPendingApplication: false).Community));
  }

  public Task<CommunityWarningResponse> IssueWarningAsync(
      string idOrSlug,
      IssueCommunityWarningRequest request,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("warning", idOrSlug));
    return Task.FromResult(JsonSerializer.Deserialize<CommunityWarningResponse>("""
        {
          "warning": {
            "community_id": "community-1",
            "created_at": "2026-01-01T00:00:00Z",
            "id": "warning-1",
            "issued_by_id": "user-1",
            "public_message": "Please read the community rules.",
            "reason": "Spam in community",
            "report_id": null
          }
        }
        """, VouchaApiJson.Options)!);
  }

  public Task<CommunityModerationResultsResponse> FetchModerationResultsAsync(
      string idOrSlug,
      string postId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(JsonSerializer.Deserialize<CommunityModerationResultsResponse>("""
          {
            "community_agent_moderations": [],
            "openai_moderation": {
              "flagged": null,
              "results": null
            }
          }
          """, VouchaApiJson.Options)!);

  public Task<CommunityMutationResponse> UpdateAsync(
      string idOrSlug,
      UpdateCommunityRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<RssFeedItemsFeedResponse> FetchNewsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<CommunityPinnedPostsResponse> FetchPinnedPostsAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<CommunityModmailMessageListResponse> FetchModmailMessagesAsync(
      string idOrSlug,
      string threadId,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<CommunityModmailThreadListResponse> FetchModmailAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task ApplyAsync(string idOrSlug, ApplyToCommunityRequest request, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("apply", idOrSlug));
    return ApplyFailure is not null ? Task.FromException(ApplyFailure) : Task.CompletedTask;
  }

  public Task SendInviteAsync(string idOrSlug, SendCommunityInviteRequest request, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("invite", idOrSlug));
    return SendInviteFailure is not null ? Task.FromException(SendInviteFailure) : Task.CompletedTask;
  }

  public Task RedeemInviteAsync(RedeemCommunityInviteRequest request, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("redeem", request.Code));
    return RedeemInviteFailure is not null ? Task.FromException(RedeemInviteFailure) : Task.CompletedTask;
  }
}

internal sealed class TestSessionStore(SessionSnapshot? current) : ISessionStore
{
  public event EventHandler<SessionChangedEventArgs>? SessionChanged
  {
    add { }
    remove { }
  }

  public SessionSnapshot Current { get; } = current ?? SessionSnapshot.Anonymous;

  public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

  public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal static class SessionSnapshotForTests
{
  public static SessionSnapshot Authenticated { get; } = new(new User(
      Id: "user-1",
      Username: "testuser",
      Roles: ["user"],
      EmailAddress: "tests@example.com",
      MembershipPlan: "free"));
}
