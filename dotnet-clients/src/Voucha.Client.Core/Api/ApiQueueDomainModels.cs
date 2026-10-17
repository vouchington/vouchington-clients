using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record DomainStateCount(int Count, bool Complete);

public sealed record QueueDomain(
    string Domain,
    bool Paused,
    [property: JsonPropertyName("paused_at")] string? PausedAt,
    [property: JsonPropertyName("paused_by")] string? PausedBy,
    string? Reason,
    DomainStateCount Waiting,
    DomainStateCount Active,
    DomainStateCount Delayed,
    DomainStateCount Completed,
    DomainStateCount Failed);

public sealed record QueueDomainsResponse(IReadOnlyList<QueueDomain> Domains);
public sealed record PauseQueueDomainBody(string? Reason);
public sealed record RetryQueueDomainResponse(bool Success, int Retried);
