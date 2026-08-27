using System.Net;

namespace Voucha.Client.Core.Chat;

/// <summary>
/// Thrown when a local-LLM endpoint responds with an HTTP redirect. Redirects are never followed
/// (<see cref="SocketsHttpHandler.AllowAutoRedirect"/> is disabled by
/// <see cref="OpenAICompatibleResponsesClient"/>'s default constructor) because following one
/// could resend the request's Authorization header to a host the user never configured. This is
/// the .NET analogue of Swift's <c>LocalLLMError.redirectNotAllowed</c>. It derives from
/// <see cref="HttpRequestException"/> so existing <c>catch (HttpRequestException)</c> sites keep
/// working unchanged, while callers that need to distinguish a suppressed redirect from any other
/// HTTP failure can still catch this type specifically.
/// </summary>
public sealed class LocalLLMRedirectNotAllowedException : HttpRequestException
{
  public LocalLLMRedirectNotAllowedException()
  {
  }

  public LocalLLMRedirectNotAllowedException(string? message)
      : base(message)
  {
  }

  public LocalLLMRedirectNotAllowedException(string? message, Exception? innerException)
      : base(message, innerException)
  {
  }

  public LocalLLMRedirectNotAllowedException(HttpStatusCode statusCode)
      : base(
          $"The local model endpoint redirected the request (status {(int)statusCode}). Configure the final endpoint directly.",
          null,
          statusCode)
  {
  }
}
