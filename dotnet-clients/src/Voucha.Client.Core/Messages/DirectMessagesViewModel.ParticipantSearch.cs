namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel
{
  // Delegates to the shared bounded-loop search in DirectMessagesViewModel.Mutations.cs, adding
  // the generation check that ResetParticipantSearchState relies on.
  public async Task SearchParticipantUsersAsync(string query, CancellationToken cancellationToken = default)
  {
    var generation = participantSearchGeneration;
    await SearchUsersAsync(
        query,
        value => latestParticipantUserSearchQuery = value,
        () => latestParticipantUserSearchQuery,
        rows => ParticipantUserResults = rows,
        cancellationToken,
        isSuperseded: () => generation != participantSearchGeneration)
        .ConfigureAwait(true);
  }
}
