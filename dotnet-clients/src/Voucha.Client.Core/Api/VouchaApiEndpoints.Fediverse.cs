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

  public static ApiRequest BeginBlueskyAccountLink(string handle, string? reAuthToken = null) =>
      new ApiRequest(HttpMethod.Post, "/api/v1/auth/bluesky/link") { Body = new BeginBlueskyAccountLinkBody(handle, reAuthToken) }
      .ConfirmWith(reAuthToken, proof => BeginBlueskyAccountLink(handle, proof));

  public static ApiRequest DisconnectBlueskyAccount(string? reAuthToken = null) =>
      new ApiRequest(HttpMethod.Delete, "/api/v1/auth/bluesky/link")
      { Body = reAuthToken is null ? null : new ReAuthTokenBody(reAuthToken) }
      .ConfirmWith(reAuthToken, proof => DisconnectBlueskyAccount(proof));

  public static ApiRequest BeginNativeBlueskyAccountLink(string handle, string completionProofChallenge, string? reAuthToken = null) =>
      new ApiRequest(HttpMethod.Post, "/api/v1/auth/bluesky/link")
      {
        Body = new BeginNativeBlueskyAccountLinkBody(handle, completionProofChallenge, ReAuthToken: reAuthToken),
      }.ConfirmWith(reAuthToken, proof => BeginNativeBlueskyAccountLink(handle, completionProofChallenge, proof));

  public static ApiRequest CompleteNativeBlueskyAccountLink(
      string flowId,
      string completionToken,
      string completionProofVerifier) =>
      new(HttpMethod.Post, "/api/v1/auth/bluesky/link-completions")
      {
        Body = new CompleteNativeBlueskyAccountLinkBody(flowId, completionToken, completionProofVerifier),
      };
}
