namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest FediverseInstances(
      string? query = null,
      string? sort = null,
      string? after = null,
      int? limit = null) =>
      Get(
          "/api/v1/fediverse/instances",
          Query(("q", query), ("sort", sort), ("after", after), ("limit", limit)));

  public static ApiRequest FediverseInstance(string idOrSlug) =>
      Get($"/api/v1/fediverse/instances/{Path(idOrSlug)}");

  public static ApiRequest BeginNativeBlueskyAccountLink(string handle, string completionProofChallenge) =>
      new(HttpMethod.Post, "/api/v1/auth/bluesky/link")
      {
        Body = new BeginNativeBlueskyAccountLinkBody(handle, completionProofChallenge),
      };

  public static ApiRequest CompleteNativeBlueskyAccountLink(
      string flowId,
      string completionToken,
      string completionProofVerifier) =>
      new(HttpMethod.Post, "/api/v1/auth/bluesky/link-completions")
      {
        Body = new CompleteNativeBlueskyAccountLinkBody(flowId, completionToken, completionProofVerifier),
      };
}
