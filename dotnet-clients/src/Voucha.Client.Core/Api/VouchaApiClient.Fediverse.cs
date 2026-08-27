namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<FediverseInstancesResponse> FetchFediverseInstancesAsync(
      string? query = null,
      string? sort = null,
      string? after = null,
      int? limit = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<FediverseInstancesResponse>(
          VouchaApiEndpoints.FediverseInstances(query, sort, after, limit),
          cancellationToken);

  public Task<FediverseInstanceResponse> FetchFediverseInstanceAsync(
      string idOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<FediverseInstanceResponse>(VouchaApiEndpoints.FediverseInstance(idOrSlug), cancellationToken);

  public Task<BeginBlueskyAccountLinkResponse> BeginNativeBlueskyAccountLinkAsync(
      string handle,
      string completionProofChallenge,
      CancellationToken cancellationToken = default) =>
      SendAsync<BeginBlueskyAccountLinkResponse>(
          VouchaApiEndpoints.BeginNativeBlueskyAccountLink(handle, completionProofChallenge),
          cancellationToken);

  public Task CompleteNativeBlueskyAccountLinkAsync(
      string flowId,
      string completionToken,
      string completionProofVerifier,
      CancellationToken cancellationToken = default) =>
      SendAsync(
          VouchaApiEndpoints.CompleteNativeBlueskyAccountLink(
              flowId,
              completionToken,
              completionProofVerifier),
          cancellationToken);
}
