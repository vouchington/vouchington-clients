namespace Voucha.Client.Core.Tags;

using Voucha.Client.Core.Api;

public sealed record TagRelationRow(
    string Id,
    string Title,
    string? Subtitle = null,
    double? NetVotes = null,
    ElectionVoteChoice? MyVote = null);

public sealed record TagSearchResultRow(
    string Id,
    string Title,
    string? Subtitle = null);
