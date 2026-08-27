using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public sealed partial class ApiCommunitiesService
{
  public Task<CommunityAutomodSimulation> SimulateAutomodAsync(
      string idOrSlug,
      CommunityAutomodSimulationRequest request,
      CancellationToken cancellationToken = default) =>
      client.SimulateCommunityAutomodAsync(idOrSlug, request, cancellationToken);

  public Task<CommunityAutomodFeedbackResponse> RecordAutomodFeedbackAsync(
      string idOrSlug,
      string sourceKey,
      CommunityAutomodFeedbackRequest request,
      CancellationToken cancellationToken = default) =>
      client.RecordCommunityAutomodFeedbackAsync(idOrSlug, sourceKey, request, cancellationToken);
}
