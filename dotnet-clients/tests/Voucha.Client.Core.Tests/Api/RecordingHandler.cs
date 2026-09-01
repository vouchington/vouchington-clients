using System.Net;
using System.Text;

namespace Voucha.Client.Core.Tests.Api;

internal sealed class RecordingHandler : HttpMessageHandler
{
  private readonly Queue<RecordedResponse> responses;
  private readonly RecordedResponse? reusableResponse;

  public RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
  {
    reusableResponse = new RecordedResponse(responseBody, statusCode);
    responses = [];
  }

  public RecordingHandler(IEnumerable<RecordedResponse> responses)
  {
    this.responses = new Queue<RecordedResponse>(responses);
  }

  private RecordedResponse NextResponse(HttpRequestMessage request)
  {
    if (reusableResponse is not null)
    {
      return reusableResponse;
    }

    return responses.Count > 0
        ? responses.Dequeue()
        : throw new InvalidOperationException(
            $"Unexpected HTTP {request.Method} {request.RequestUri?.PathAndQuery}: " +
            "no more recorded responses available.");
  }

  public HttpMethod? Method { get; private set; }

  public string? PathAndQuery { get; private set; }

  public string? RequestBody { get; private set; }

  public string? IdempotencyKey { get; private set; }

  public List<RecordedRequest> Requests { get; } = [];

  protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
  {
    Method = request.Method;
    PathAndQuery = request.RequestUri?.PathAndQuery;
    IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
    RequestBody = request.Content is null
        ? null
        : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

    Requests.Add(new RecordedRequest(Method, PathAndQuery, RequestBody));

    var response = NextResponse(request);
    return new HttpResponseMessage(response.StatusCode)
    {
      Content = new StringContent(response.Body, Encoding.UTF8, "application/json"),
      RequestMessage = request,
    };
  }
}

internal sealed record RecordedRequest(HttpMethod? Method, string? PathAndQuery, string? Body);

internal sealed record RecordedResponse(string Body, HttpStatusCode StatusCode = HttpStatusCode.OK);
