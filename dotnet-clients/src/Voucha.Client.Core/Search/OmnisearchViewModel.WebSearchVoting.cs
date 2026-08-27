using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Voting;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  private ElectionVoteChoice? selectedHostnameVote;
  private readonly HashSet<string> votingHostnameIds = [];

  public ElectionVoteChoice? SelectedHostnameVote => selectedHostnameVote;

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public bool CanClearSelectedHostnameVote =>
      selectedHostnameId is { } id &&
      selectedHostnameVote is not null &&
      ViewerIsAuthenticated &&
      !ViewerCanCastPublicVotes &&
      !votingHostnameIds.Contains(id);

  public async Task VoteSelectedHostnameAsync(ElectionVoteChoice? choice, CancellationToken cancellationToken = default)
  {
    if (selectedHostnameId is not { } id ||
        (choice is null ? !CanClearSelectedHostnameVote : !CanVoteSelectedHostname)) return;
    if (!votingHostnameIds.Add(id)) return;

    var previous = (selectedHostnameDetail, selectedHostnameVote);
    var mutationSearchRequestId = searchRequestId;
    ApplySelectedHostnameVote(choice);

    try
    {
      await EmailVerificationGate.RunAsync(
          async () =>
          {
            if (choice is { } selected)
            {
              await client.VoteHostnameAsync(id, selected, cancellationToken).ConfigureAwait(true);
            }
            else
            {
              await client.ClearHostnameVoteAsync(id, cancellationToken).ConfigureAwait(true);
            }
          },
          exception => RestoreSelectedHostnameVote(mutationSearchRequestId, id, previous, exception.Message))
          .ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RestoreSelectedHostnameVote(mutationSearchRequestId, id, previous, null);
      throw;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      RestoreSelectedHostnameVote(mutationSearchRequestId, id, previous, ex.Message);
    }
    finally
    {
      votingHostnameIds.Remove(id);
      if (selectedHostnameId == id)
      {
        RefreshSelectedHostnameGroups();
      }
    }
  }

  private void ApplySelectedHostnameVote(ElectionVoteChoice? choice)
  {
    var detail = selectedHostnameDetail;
    if (detail is null) return;
    var election = detail.HostnameElection ?? new HostnameElection(
        "hostname_election", detail.Hostname.Id, 0, 0, 0);
    var vote = VoteCalculator.Apply(
        selectedHostnameVote,
        election.VotesScoreNet,
        election.VotesCountUp,
        election.VotesCountDown,
        choice);
    selectedHostnameVote = vote.Current;
    selectedHostnameDetail = detail with
    {
      HostnameElection = election with
      {
        VotesScoreNet = vote.Net ?? election.VotesScoreNet,
        VotesCountUp = vote.Up ?? election.VotesCountUp,
        VotesCountDown = vote.Down ?? election.VotesCountDown,
      },
    };
    RefreshSelectedHostnameGroups();
  }

  private void RestoreSelectedHostnameVote(
      int mutationSearchRequestId,
      string hostnameId,
      (HostnameDetailResponse? Detail, ElectionVoteChoice? Choice) previous,
      string? errorMessage)
  {
    if (searchRequestId != mutationSearchRequestId || selectedHostnameId != hostnameId) return;
    selectedHostnameDetail = previous.Detail;
    selectedHostnameVote = previous.Choice;
    RefreshSelectedHostnameGroups();
    ErrorMessage = errorMessage;
  }

  private void RefreshSelectedHostnameGroups()
  {
    if (selectedHostnameDetail is not null && remapGroups is not null) Groups = remapGroups();
    OnPropertyChanged(nameof(CanClearSelectedHostnameVote));
  }
}
