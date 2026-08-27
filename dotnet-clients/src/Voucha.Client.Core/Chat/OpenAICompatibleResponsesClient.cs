using System.Net;
using System.Net.Http.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Chat;

public sealed class OpenAICompatibleResponsesClient : IDisposable
{
  private readonly HttpClient httpClient;

  /// <summary>The only public production entry point: it always builds its handler via
  /// <see cref="CreatePinnedHandler"/>, so it can never be fed an <c>HttpClientHandler</c> or a
  /// <c>DelegatingHandler</c> wrapping an unpinned <see cref="SocketsHttpHandler"/> -- a gap a
  /// public handler-accepting constructor cannot close by inspecting the handler at runtime, since
  /// that check only recognizes a directly-passed, unwrapped <see cref="SocketsHttpHandler"/>.
  /// Custom handler injection (test doubles, recording handlers) stays on the internal constructor
  /// below, reachable only from this assembly and its test project.</summary>
  [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The delegated constructor transfers handler ownership to HttpClient.")]
  public OpenAICompatibleResponsesClient(TimeSpan? timeout = null)
      : this(CreatePinnedHandler(), timeout)
  {
  }

  /// <summary>The only sanctioned way to build a handler for this client: it always re-validates a
  /// cleartext connection's actually-resolved peer via <see cref="LocalLLMConnectionPinning"/>, closing
  /// the DNS-rebinding-shaped gap between when a local-model endpoint is saved and when it is dialed.
  /// Public so the internal handler-accepting constructor's test-project callers can build a pinned
  /// handler explicitly when they need one (e.g. to pass alongside a custom timeout in a test double
  /// scenario); ordinary production callers should prefer the public (<see cref="TimeSpan"/>?)
  /// constructor above, which already routes through this factory.</summary>
  public static SocketsHttpHandler CreatePinnedHandler() => new()
  {
    AllowAutoRedirect = false,
    Proxy = new CleartextBypassProxy(HttpClient.DefaultProxy),
    ConnectCallback = LocalLLMConnectionPinning.ConnectAsync,
  };

  /// <summary>Routes only cleartext (<c>http://</c>) requests around the system/corporate proxy;
  /// HTTPS requests still see whatever proxy the system would normally use. A forward proxy never
  /// triggers <see cref="SocketsHttpHandler.ConnectCallback"/> against the real origin -- it connects
  /// to the proxy instead -- which would silently defeat <see cref="LocalLLMConnectionPinning"/>'s
  /// connect-time re-validation for a private-network endpoint. HTTPS is unaffected by that gap:
  /// <see cref="LocalLLMConnectionPinning"/> intentionally skips its re-check there, since TLS plus
  /// the saved-hostname policy already gate it, so forcing HTTPS traffic off the proxy too would only
  /// break legitimate corporate/system-proxy setups for public model endpoints without adding any
  /// security value.</summary>
  internal sealed class CleartextBypassProxy(IWebProxy inner) : IWebProxy
  {
    // Backed by a local field rather than writing through to `inner`: `inner` is typically
    // HttpClient.DefaultProxy, a static process-wide instance shared by every HttpClient in the
    // app. Writing through it would leak this client's credentials into unrelated HttpClients.
    private ICredentials? credentialsOverride;

    public ICredentials? Credentials
    {
      get => credentialsOverride ?? inner.Credentials;
      set => credentialsOverride = value;
    }

    public Uri? GetProxy(Uri destination) =>
        destination.Scheme == Uri.UriSchemeHttp ? null : inner.GetProxy(destination);

    public bool IsBypassed(Uri host) => host.Scheme == Uri.UriSchemeHttp || inner.IsBypassed(host);
  }

  /// <summary>Internal rather than public: a public overload accepting any <see cref="HttpMessageHandler"/>
  /// can only ever runtime-check for a directly-passed, unwrapped <see cref="SocketsHttpHandler"/> --
  /// an <c>HttpClientHandler</c> or a <c>DelegatingHandler</c> wrapping an unpinned
  /// <see cref="SocketsHttpHandler"/> would sail through undetected. Restricting this to the assembly
  /// and its test project (via <c>InternalsVisibleTo</c>) makes the public surface structurally
  /// incapable of accepting an arbitrary handler at all, which the runtime check below cannot
  /// guarantee on its own.</summary>
  internal OpenAICompatibleResponsesClient(HttpMessageHandler handler, TimeSpan? timeout = null)
  {
    ArgumentNullException.ThrowIfNull(handler);
    // Defense in depth against a same-assembly/test-project caller hand-rolling a SocketsHttpHandler
    // by mistake: this check is exact and unconditional -- it inspects the actual object passed in,
    // so it still catches an unpinned SocketsHttpHandler regardless of how it was spelled at the call
    // site. A non-SocketsHttpHandler (e.g. a test double) is unaffected. The ast-grep gate
    // (cs-no-raw-local-llm-handler.yml) covers the same case at the source-text level for production
    // code, since a src/-file caller now needs InternalsVisibleTo access to even reach here.
    if (handler is SocketsHttpHandler sh && sh.ConnectCallback != LocalLLMConnectionPinning.ConnectAsync)
    {
      throw new ArgumentException(
          "Use OpenAICompatibleResponsesClient.CreatePinnedHandler() -- it wires the DNS-rebinding guard.",
          nameof(handler));
    }

    httpClient = new HttpClient(handler, disposeHandler: true)
    {
      Timeout = timeout ?? TimeSpan.FromSeconds(100),
    };
  }

  public async Task<string?> GenerateAssistantContentAsync(
      string message,
      IReadOnlyList<LocalLLMResponseInput> history,
      LocalLLMEndpointProfile profile,
      string? apiKey,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(message);
    ArgumentNullException.ThrowIfNull(history);
    ArgumentNullException.ThrowIfNull(profile);

    var uri = profile.ResponsesUri ?? throw new InvalidOperationException("Enter a valid Responses API endpoint.");
    var model = profile.SelectedModel ?? throw new InvalidOperationException("Choose a local model.");
    using var request = new HttpRequestMessage(HttpMethod.Post, uri);
    request.Headers.Accept.ParseAdd("application/json");
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
      request.Headers.Authorization = new("Bearer", apiKey.Trim());
    }

    request.Content = JsonContent.Create(new ResponsesRequest(
        model,
        LocalLLMHistoryBudget.BuildEndpointInput(history, message)));

    using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if ((int)response.StatusCode is >= 300 and < 400)
    {
      throw new LocalLLMRedirectNotAllowedException(response.StatusCode);
    }
    if (!response.IsSuccessStatusCode)
    {
      throw new HttpRequestException(
          $"The local model request failed with status {(int)response.StatusCode}.",
          null,
          response.StatusCode);
    }

    ResponsesPayload? payload;
    try
    {
      payload = await response.Content
          .ReadFromJsonAsync<ResponsesPayload>(cancellationToken: cancellationToken)
          .ConfigureAwait(false);
    }
    catch (JsonException ex)
    {
      throw new InvalidOperationException("The local model returned an invalid JSON response.", ex);
    }
    return payload?.TextOutput();
  }

  public void Dispose() => httpClient.Dispose();

  private sealed record ResponsesRequest(
      [property: JsonPropertyName("model")] string Model,
      [property: JsonPropertyName("input")] IReadOnlyList<LocalLLMResponseInput> Input);

  private sealed record ResponsesPayload(
      [property: JsonPropertyName("output_text")] string? OutputText,
      [property: JsonPropertyName("output")] IReadOnlyList<ResponsesOutput>? Output)
  {
    public string? TextOutput()
    {
      if (!string.IsNullOrWhiteSpace(OutputText))
      {
        return OutputText.Trim();
      }
      var text = string.Concat(Output?.SelectMany(item => item.Content ?? []).Select(item => item.Text) ?? []);
      return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
  }

  private sealed record ResponsesOutput(
      [property: JsonPropertyName("content")] IReadOnlyList<ResponsesContent>? Content);

  private sealed record ResponsesContent([property: JsonPropertyName("text")] string? Text);
}
