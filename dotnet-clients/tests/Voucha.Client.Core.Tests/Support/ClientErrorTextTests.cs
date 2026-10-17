using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class ClientErrorTextTests
{
  private static readonly UiMessageKey Fallback = UiMessageKey.NativeSwiftMembershipPlansLoadFailure;

  [Theory]
  [InlineData(400, "{\"message\":\"Choose another SKU\"}", "Choose another SKU")]
  [InlineData(499, "{\"error\":{\"message\":\"Try a shorter query\"}}", "Try a shorter query")]
  public void ClientStatusesExposeTheServerMessage(int status, string body, string expected)
  {
    var text = ClientErrorText.For(new VouchaApiException((HttpStatusCode)status, body), Fallback);

    Assert.Null(text.Key);
    Assert.Equal(expected, text.VerbatimValue);
  }

  [Theory]
  [InlineData(399, "{\"message\":\"Below the client range\"}")]
  [InlineData(500, "{\"message\":\"Internal implementation detail\"}")]
  [InlineData(400, "{\"message\":\"   \"}")]
  [InlineData(422, null)]
  public void OtherApiFailuresStayLocalized(int status, string? body)
  {
    var text = ClientErrorText.For(new VouchaApiException((HttpStatusCode)status, body), Fallback);

    Assert.Equal(Fallback, text.Key);
    Assert.Null(text.VerbatimValue);
  }

  [Fact]
  public void ExceptionsWithoutAClientStatusStayLocalized()
  {
    Assert.Equal(Fallback, ClientErrorText.For(new VouchaApiException("plain"), Fallback).Key);
    Assert.Equal(Fallback, ClientErrorText.For(new HttpRequestException("offline"), Fallback).Key);
    Assert.Equal(Fallback, ClientErrorText.For(new InvalidOperationException("closed"), Fallback).Key);
  }
}
