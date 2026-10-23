using Voucha.Client.Core;
using Xunit;

namespace Voucha.Client.Core.Tests;

public sealed class SentryEventScrubberTests
{
  [Fact]
  public void MissingDsnDoesNotSend()
  {
    var transport = new RecordingSentryTransport();
    var plan = SentryLaunchPlan.Resolve(null, null, releaseConfiguration: false, "1.0+1", null);

    var runtime = SentryRuntime.Open(plan, transport);

    Assert.Null(runtime);
    Assert.Empty(transport.Events);
  }

  [Fact]
  public void ConfiguredDsnSendsOneScrubbedEventThroughTheStubTransport()
  {
    var transport = new RecordingSentryTransport();
    var plan = SentryLaunchPlan.Resolve(
        new Dictionary<string, string?> { ["VOUCHA_SENTRY_DSN"] = "https://public@example.invalid/1" },
        null,
        releaseConfiguration: false,
        "1.0+1",
        "account-1");
    var runtime = SentryRuntime.Open(plan, transport);
    Assert.NotNull(runtime);

    runtime.Capture(new SentryOutboundEvent(
        " account-1 ",
        "person@example.invalid",
        "person",
        "203.0.113.10",
        "https://voucha.ai/api/v1/session?code=123456",
        "code=123456",
        new Dictionary<string, string>
        {
          ["Authorization"] = "Bearer secret",
          ["X-App-Attest-Assertion"] = "assertion",
          ["Accept"] = "application/json",
        },
        "dt=device; st=session",
        """{"code":"123456"}""",
        new Dictionary<string, string>
        {
          ["chat"] = "hello",
          ["email_code"] = "123456",
          ["screen"] = "session",
        },
        [
          new SentryBreadcrumbDraft("ui.input", "typed secret", new Dictionary<string, string> { ["text"] = "secret" }),
          new SentryBreadcrumbDraft("http", "GET", new Dictionary<string, string>
          {
            ["url"] = "https://voucha.ai/api/v1/session?code=123456",
            ["http.query"] = "code=123456",
            ["http.fragment"] = "token=secret",
          }),
        ]));

    var sent = Assert.Single(transport.Events);
    Assert.Equal("account-1", sent.AccountId);
    Assert.Null(sent.Email);
    Assert.Null(sent.Username);
    Assert.Null(sent.IpAddress);
    Assert.Equal("https://voucha.ai/api/v1/session", sent.RequestTarget);
    Assert.Null(sent.QueryString);
    Assert.Equal(SentryEventScrubber.Filtered, sent.Headers?["Authorization"]);
    Assert.Equal(SentryEventScrubber.Filtered, sent.Headers?["X-App-Attest-Assertion"]);
    Assert.Equal("application/json", sent.Headers?["Accept"]);
    Assert.Equal(SentryEventScrubber.Filtered, sent.Cookies);
    Assert.Null(sent.Body);
    Assert.Equal("session", sent.Extras?["screen"]);
    Assert.False(sent.Extras?.ContainsKey("chat"));
    Assert.False(sent.Extras?.ContainsKey("email_code"));
    var breadcrumb = Assert.Single(sent.Breadcrumbs!);
    Assert.Equal("http", breadcrumb.Category);
    Assert.Equal("https://voucha.ai/api/v1/session", breadcrumb.Data?["url"]);
    Assert.False(breadcrumb.Data?.ContainsKey("http.query"));
    Assert.False(breadcrumb.Data?.ContainsKey("http.fragment"));
  }

  [Fact]
  public void SpanAddressAndQueryFieldsAreScrubbed()
  {
    Assert.Equal(
        "GET https://voucha.ai/search",
        SentryTelemetryFields.ScrubAddress("GET https://voucha.ai/search?q=private#token"));
    Assert.Null(SentryTelemetryFields.ScrubField("http.query", "q=private-search"));
    Assert.Null(SentryTelemetryFields.ScrubField("http.query_string", "token=secret"));
    Assert.Null(SentryTelemetryFields.ScrubField("http.fragment", "token=secret"));
    Assert.Equal(
        "https://voucha.ai/search",
        SentryTelemetryFields.ScrubField("url", "https://voucha.ai/search?q=private-search"));
    Assert.Equal("GET", SentryTelemetryFields.ScrubField("method", "GET"));
    Assert.True(SentryTelemetryFields.KeepsNumber("status_code"));
    Assert.True(SentryTelemetryFields.KeepsNumber("http.response.status_code"));
    Assert.False(SentryTelemetryFields.KeepsNumber("http.query"));
    var scrubbed = SentryEventScrubber.Scrub(new SentryOutboundEvent(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        new Dictionary<string, string> { ["request_url"] = "https://voucha.ai/search?q=private" },
        [new SentryBreadcrumbDraft("http", "GET /search?q=private", null)]));
    Assert.Equal("https://voucha.ai/search", scrubbed.Extras?["request_url"]);
    Assert.Equal("GET /search", Assert.Single(scrubbed.Breadcrumbs!).Message);
  }

  private sealed class RecordingSentryTransport : ISentryEventTransport
  {
    public List<SentryOutboundEvent> Events { get; } = [];

    public void Send(SentryOutboundEvent scrubbed) => Events.Add(scrubbed);
  }
}
