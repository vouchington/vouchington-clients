using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Copyright;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.Copyright;

public sealed partial class CopyrightNoticesViewModelTests
{
  private static CopyrightNoticesViewModel Model(CaseService service) => new(service, new NavigationViewer(true, []));
  private static T Fixture<T>(string id) => JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(id), VouchaApiJson.Options)!;

  private sealed class CaseService : ICopyrightNoticesService
  {
    public List<string> Calls { get; } = [];
    public Func<string?, int, CancellationToken, Task<CopyrightNoticesResponse>> Page { get; init; } =
        (_, _, _) => Task.FromResult(Fixture<CopyrightNoticesResponse>("web.copyright.notices.default"));
    public Func<string, CancellationToken, Task<CopyrightNoticeResponse>> Detail { get; init; } =
        (_, _) => Task.FromResult(Fixture<CopyrightNoticeResponse>("web.copyright.notice.detail.populated"));
    public Func<string, CancellationToken, Task<CopyrightParticipantNoticeResponse>> Participant { get; init; } =
        (_, _) => Task.FromResult(Fixture<CopyrightParticipantNoticeResponse>("web.copyright.notice.participant.populated"));
    public Func<string, string, int, CancellationToken, Task<CopyrightEuDisputeSettlementsResponse>> Settlements { get; init; } =
        (_, _, _, _) => Task.FromResult(Fixture<CopyrightEuDisputeSettlementsResponse>("web.copyright.eu.dispute-settlements.participant"));

    public Task<CopyrightNoticesResponse> FetchPageAsync(string? after, int limit, CancellationToken cancellationToken)
    {
      Calls.Add($"list:{after}:{limit}");
      return Page(after, limit, cancellationToken);
    }
    public Task<CopyrightNoticeResponse> FetchDetailAsync(string id, CancellationToken cancellationToken)
    {
      Calls.Add($"detail:{id}");
      return Detail(id, cancellationToken);
    }
    public Task<CopyrightParticipantNoticeResponse> FetchParticipantAsync(string id, CancellationToken cancellationToken)
    {
      Calls.Add($"participant:{id}");
      return Participant(id, cancellationToken);
    }
    public Task<CopyrightEuDisputeSettlementsResponse> FetchSettlementsAsync(string id, string after, int limit, CancellationToken cancellationToken)
    {
      Calls.Add($"settlements:{id}:{after}:{limit}");
      return Settlements(id, after, limit, cancellationToken);
    }
  }
}
