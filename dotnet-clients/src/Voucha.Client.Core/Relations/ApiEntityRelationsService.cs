using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Relations;

public sealed class ApiEntityRelationsService : IEntityRelationsService
{
  private readonly VouchaApiClient client;

  public ApiEntityRelationsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<EntityRelationsResponse> FetchAsync(
      EntityRelationsRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchEntityRelationsAsync(request, cancellationToken);

  public Task<EntityRelationResponse> CreateAsync(
      CreateEntityRelationRequest request,
      CancellationToken cancellationToken = default) =>
      client.CreateEntityRelationAsync(request, cancellationToken);
}
