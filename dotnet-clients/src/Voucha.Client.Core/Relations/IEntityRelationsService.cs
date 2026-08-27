using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Relations;

public interface IEntityRelationsService
{
  Task<EntityRelationsResponse> FetchAsync(
      EntityRelationsRequest request,
      CancellationToken cancellationToken = default);

  Task<EntityRelationResponse> CreateAsync(
      CreateEntityRelationRequest request,
      CancellationToken cancellationToken = default);
}
