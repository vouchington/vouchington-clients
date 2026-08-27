using System.Net;

namespace Voucha.Client.Core.Api;

internal sealed class VouchaApiDecodeException : HttpRequestException
{
  public VouchaApiDecodeException() { }
  public VouchaApiDecodeException(string message) : base(message) { }
  public VouchaApiDecodeException(string message, Exception inner) : base(message, inner) { }

  public VouchaApiDecodeException(HttpStatusCode statusCode, string message, Exception? inner = null)
      : base(message, inner, statusCode) { }
}
