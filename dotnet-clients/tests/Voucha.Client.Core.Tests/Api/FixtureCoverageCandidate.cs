namespace Voucha.Client.Core.Tests.Api;

/// <summary>A manifest fixture a client must account for, with the producer's <c>pending</c> flag.</summary>
internal sealed record FixtureCoverageCandidate(string Id, bool Pending);
