using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Profiles;

public sealed record ProfileUserTagRow(
    string Id,
    string Title,
    int VoteCountUp,
    int VoteCountDown,
    ElectionVoteChoice? MyVote);

public sealed partial class ProfileViewModel
{
  private static readonly string[] UserTagTitleKeys = ["label", "name", "title", "slug"];
  private VouchaApiClient? userTagClient;
  private IReadOnlyList<ProfileUserTagRow> userTags = [];
  private readonly HashSet<string> userTagVotesInFlight = new(StringComparer.Ordinal);
  private int userTagLoadVersion;

  public IReadOnlyList<ProfileUserTagRow> UserTags
  {
    get => userTags;
    private set
    {
      if (SetProperty(ref userTags, value)) userTagPages.ReplaceItems(value);
    }
  }

  public bool CanViewUserTags => CanActOnUser;

  public bool CanCreateUserTagVotes =>
      CanViewUserTags && IsSignedInViewer && currentViewerCanCreateUserTagVotes;

  public bool CanClearUserTagVotes => CanViewUserTags && IsSignedInViewer;

  public void ConfigureUserTagClient(VouchaApiClient client) =>
      userTagClient = client ?? throw new ArgumentNullException(nameof(client));

  public async Task LoadUserTagsAsync(CancellationToken cancellationToken = default)
  {
    var loadVersion = ++userTagLoadVersion;
    userTagPages.Reset();
    NotifyUserTagPagination();
    if (!CanViewUserTags || User?.Id is not { Length: > 0 } userId || userTagClient is null)
    {
      UserTags = [];
      return;
    }
    UserTags = [];
    try
    {
      var response = await userTagClient.FetchEntityRelationsAsync(
          new EntityRelationsRequest("user", userId, "category", "topic", PositiveNetVoteScore: true, Limit: 25),
          cancellationToken).ConfigureAwait(true);
      if (loadVersion == userTagLoadVersion && User?.Id == userId) ReplaceUserTagPage(response);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (loadVersion == userTagLoadVersion) UserTags = [];
    }
  }

  public async Task VoteUserTagAsync(string relationId, ElectionVoteChoice? choice, CancellationToken cancellationToken = default)
  {
    if ((choice is null ? !CanClearUserTagVotes : !CanCreateUserTagVotes) ||
        userTagClient is null || string.IsNullOrWhiteSpace(relationId) ||
        !BeginUserTagVote(relationId)) return;
    try
    {
      if (choice is { } selected)
      {
        await userTagClient.VoteEntityRelationAsync(relationId, selected, cancellationToken).ConfigureAwait(true);
      }
      else
      {
        await userTagClient.ClearEntityRelationVoteAsync(relationId, cancellationToken).ConfigureAwait(true);
      }
      await LoadUserTagsAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      // Preserve the prior summary for retry.
    }
    finally
    {
      EndUserTagVote(relationId);
    }
  }

  private void ResetUserTags()
  {
    userTagLoadVersion++;
    userTagPages.Reset();
    UserTags = [];
    NotifyUserTagPagination();
  }

  private bool BeginUserTagVote(string relationId)
  {
    lock (userTagVotesInFlight) return userTagVotesInFlight.Add(relationId);
  }

  private void EndUserTagVote(string relationId)
  {
    lock (userTagVotesInFlight) userTagVotesInFlight.Remove(relationId);
  }
}
