namespace Voucha.Client.Core.ModerationIntegrity;

public sealed record IntegrityQueuePage<T>(
    IReadOnlyList<T> Results,
    string? EndCursor,
    bool HasNextPage);
