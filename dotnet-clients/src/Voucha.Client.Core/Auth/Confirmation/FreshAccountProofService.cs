using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public interface IReAuthProviderAssertion
{
  Task<string> GetCredentialAsync(string provider, string nonce, CancellationToken cancellationToken);
}

public interface IFreshAccountProofService
{
  Task<ReAuthMethodsResponse> DiscoverAsync(CancellationToken cancellationToken);
  Task RequestEmailAsync(CancellationToken cancellationToken);
  Task<string> VerifyCodeAsync(bool email, string code, CancellationToken cancellationToken);
  Task<ConfirmationPasskeyPage> FetchPasskeysAsync(string? after, CancellationToken cancellationToken);
  Task<string> VerifyPasskeyAsync(string id, CancellationToken cancellationToken);
  Task<string> VerifyProviderAsync(string provider, CancellationToken cancellationToken);
}

public sealed record ConfirmationPasskey(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name);

public sealed record ConfirmationPasskeyPage(
    [property: JsonPropertyName("results")] IReadOnlyList<ConfirmationPasskey> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed class FreshAccountProofService(
    VouchaApiClient client,
    IPasskeyAssertionProvider passkeys,
    IReAuthProviderAssertion sdk,
    FreshAccountBroker broker) : IFreshAccountProofService
{
  public Task<ReAuthMethodsResponse> DiscoverAsync(CancellationToken cancellationToken) =>
      client.SendAsync<ReAuthMethodsResponse>(VouchaApiEndpoints.ReAuthMethods(), cancellationToken);

  public Task RequestEmailAsync(CancellationToken cancellationToken) =>
      client.SendAsync(VouchaApiEndpoints.RequestReAuthEmailToken(), cancellationToken);

  public async Task<string> VerifyCodeAsync(bool email, string code, CancellationToken cancellationToken) =>
      (await client.SendAsync<ReAuthTokenResponse>(email
          ? VouchaApiEndpoints.VerifyReAuthEmailCode(code)
          : VouchaApiEndpoints.VerifyReAuthTotp(code), cancellationToken).ConfigureAwait(false)).ReAuthToken;

  public Task<ConfirmationPasskeyPage> FetchPasskeysAsync(string? after, CancellationToken cancellationToken) =>
      client.SendAsync<ConfirmationPasskeyPage>(new ApiRequest(HttpMethod.Get, "/api/v1/auth/passkeys")
      {
        Query = after is null ? new Dictionary<string, string> { ["limit"] = "25" }
            : new Dictionary<string, string> { ["limit"] = "25", ["after"] = after },
      }, cancellationToken);

  public async Task<string> VerifyPasskeyAsync(string id, CancellationToken cancellationToken)
  {
    var response = await client.SendAsync<ReAuthPasskeyOptionsResponse>(
        VouchaApiEndpoints.ReAuthPasskeyOptions(id), cancellationToken).ConfigureAwait(true);
    var options = response.Options;
    if (string.IsNullOrWhiteSpace(response.FlowId) || options is null || string.IsNullOrWhiteSpace(options.Challenge) ||
        string.IsNullOrWhiteSpace(options.RpId) || options.AllowCredentials is not { Count: > 0 } ||
        options.AllowCredentials.Any(item => string.IsNullOrWhiteSpace(item.Id) || item.Type != "public-key"))
      throw new InvalidOperationException();
    var assertion = await passkeys.GetAssertionAsync(new PasskeyAuthenticationOptions(
        options.Challenge, options.RpId, options.UserVerification,
        options.AllowCredentials?.Select(item => new PasskeyCredentialDescriptor(item.Id)).ToArray()),
        cancellationToken).ConfigureAwait(false);
    return (await client.SendAsync<ReAuthTokenResponse>(VouchaApiEndpoints.VerifyReAuthPasskey(
        id, response.FlowId, JsonSerializer.SerializeToElement(assertion, VouchaApiJson.Options)),
        cancellationToken).ConfigureAwait(false)).ReAuthToken;
  }

  public async Task<string> VerifyProviderAsync(string provider, CancellationToken cancellationToken)
  {
    if (provider is not ("apple" or "google"))
      return await broker.VerifyAsync(provider, cancellationToken).ConfigureAwait(false);
    var challenge = await client.SendAsync<ReAuthProviderChallengeResponse>(
        VouchaApiEndpoints.ReAuthProviderChallenge(provider), cancellationToken).ConfigureAwait(true);
    if (string.IsNullOrWhiteSpace(challenge.FlowId) || string.IsNullOrWhiteSpace(challenge.Nonce) ||
        challenge.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException();
    var credential = await sdk.GetCredentialAsync(provider, challenge.Nonce, cancellationToken).ConfigureAwait(false);
    cancellationToken.ThrowIfCancellationRequested();
    if (challenge.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException();
    return (await client.SendAsync<ReAuthTokenResponse>(VouchaApiEndpoints.VerifyReAuthProvider(
        provider, challenge.FlowId, credential), cancellationToken).ConfigureAwait(false)).ReAuthToken;
  }
}
