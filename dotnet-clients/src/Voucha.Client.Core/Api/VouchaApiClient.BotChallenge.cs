using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  private async Task<HttpResponseMessage> SendMutationAsync(ApiRequest request, CancellationToken cancellationToken)
  {
    var path = BuildUri(request);
    var uri = httpClient.BaseAddress is { } origin ? new Uri(origin, path) : new Uri(path, UriKind.RelativeOrAbsolute);
    var method = request.Method;
    var headers = request.Headers.ToArray();
    var bytes = request.Body is null ? null : JsonSerializer.SerializeToUtf8Bytes(request.Body, VouchaApiJson.Options);
    var mayChallenge = request.AllowsBotChallenge && httpClient.BaseAddress is { } apiOrigin &&
        uri.IsAbsoluteUri && uri.Scheme == apiOrigin.Scheme && uri.Host == apiOrigin.Host && uri.Port == apiOrigin.Port &&
        (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch || method == HttpMethod.Delete);
    using var scope = mayChallenge ? botChallengeReplay?.Begin(cancellationToken) : null;
    var token = scope?.Token ?? cancellationToken;

    HttpRequestMessage CreateMessage(string? proof = null)
    {
      var message = new HttpRequestMessage(method, uri);
      foreach (var (name, value) in headers) message.Headers.TryAddWithoutValidation(name, value);
      if (bytes is not null)
      {
        message.Content = new ByteArrayContent(bytes);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
      }
      if (proof is not null)
      {
        message.Headers.Remove(BotChallengeReplay.TokenHeader);
        message.Headers.TryAddWithoutValidation(BotChallengeReplay.TokenHeader, proof);
      }
      return message;
    }

    using var original = CreateMessage();
    var response = await httpClient.SendAsync(original, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
    if (scope is null || response.StatusCode != HttpStatusCode.Forbidden) return response;
    try
    {
      var body = await ReadErrorBodyAsync(response.Content, token).ConfigureAwait(false);
      // Retain the bounded error body for the normal API error path when no replay is permitted.
      response.Content.Dispose();
      response.Content = new StringContent(body ?? string.Empty);
      if (!IsBotChallenge(body)) return response;
      var proof = await scope.SolveAsync().ConfigureAwait(false);
      if (string.IsNullOrWhiteSpace(proof)) return response;
      token.ThrowIfCancellationRequested();
      using var replay = CreateMessage(proof);
      response.Dispose();
      return await httpClient.SendAsync(replay, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
    }
    catch
    {
      response.Dispose();
      throw;
    }
  }

  private static bool IsBotChallenge(string? body)
  {
    if (string.IsNullOrEmpty(body)) return false;
    try
    {
      var challenge = JsonSerializer.Deserialize<BotChallengeRequiredResponse>(body, VouchaApiJson.Options);
      return challenge?.Code == BotChallengeRequiredResponse.RequiredCode && challenge.Action == BotChallengeReplay.Action;
    }
    catch (JsonException)
    {
      return false;
    }
  }
}
