using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpFinancialProfileTests
{
  [Theory]
  [InlineData("native.mcp.get-my-financial-profile.null")]
  [InlineData("native.mcp.get-my-financial-profile.populated")]
  public async Task CallsFinancialToolAndDecodesCanonicalStructuredResult(string caseId)
  {
    using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(
        FilamentsContractPaths.ApiFixture("mcp-results.json"), TestContext.Current.CancellationToken));
    Assert.Equal(1, fixture.RootElement.GetProperty("version").GetInt32());
    var selected = fixture.RootElement.GetProperty("cases").EnumerateArray()
        .Single(value => value.GetProperty("id").GetString() == caseId);
    using var handler = new FinancialToolHandler(selected.GetProperty("structuredContent"));
    using var tokenClient = TokenClient(handler);
    using var transport = new MemberMcpClient(Origin, handler);
    var authorized = new MemberMcpAuthorizedClient(
        transport, new MemberMcpTokenManager("member-1", new TokenStore(), tokenClient));

    var result = await authorized.GetMyFinancialProfileAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Bearer known-access", handler.Authorization);
    Assert.Equal("tools/call", handler.Method);
    Assert.Equal("get_my_financial_profile", handler.ToolName);
    Assert.Equal("{}", handler.Arguments);
    if (caseId.EndsWith(".null", StringComparison.Ordinal))
      Assert.Null(result.FinancialProfile);
    else
    {
      var profile = Assert.IsType<MemberFinancialProfile>(result.FinancialProfile);
      var expected = selected.GetProperty("structuredContent").GetProperty("result")
          .GetProperty("financial_profile");
      Assert.Equal(expected.GetProperty("individual_id").GetString(), profile.IndividualId);
      Assert.Equal(expected.GetProperty("currency").GetString(), profile.Currency);
      Assert.Equal(expected.GetProperty("updated_at").GetDateTimeOffset(), profile.UpdatedAt);
      Assert.Equal("670-739", profile.CreditScoreRange);
      Assert.Equal<long?>(7_500_000, profile.StatedIncomeRange?.Minimum.Amount);
      Assert.Equal<long?>(10_000_000, profile.StatedIncomeRange?.Maximum?.Amount);
      Assert.Equal("usd", profile.StatedIncomeRange?.Minimum.Currency);
      Assert.Equal<double?>(7, profile.YearsOfCreditHistory);
      Assert.Null(profile.TotalCreditLimit);
      Assert.Null(profile.HardInquiries12m);
    }
  }

  [Theory]
  [InlineData("fractional-year")]
  [InlineData("null-identity")]
  [InlineData("missing-maximum")]
  public async Task DecodesFractionalYearButRejectsInvalidRequiredFields(string variant)
  {
    using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(
        FilamentsContractPaths.ApiFixture("mcp-results.json"), TestContext.Current.CancellationToken));
    var populated = fixture.RootElement.GetProperty("cases").EnumerateArray()
        .Single(value => value.GetProperty("id").GetString() ==
            "native.mcp.get-my-financial-profile.populated");
    var content = JsonNode.Parse(populated.GetProperty("structuredContent").GetRawText())!;
    var profileNode = content["result"]!["financial_profile"]!;
    if (variant == "null-identity") profileNode["individual_id"] = null;
    else if (variant == "missing-maximum")
      profileNode["stated_income_range"]!.AsObject().Remove("maximum");
    else profileNode["years_of_credit_history"] = 7.5;
    using var document = JsonDocument.Parse(content.ToJsonString());
    using var handler = new FinancialToolHandler(document.RootElement);
    using var tokenClient = TokenClient(handler);
    using var transport = new MemberMcpClient(Origin, handler);
    var authorized = new MemberMcpAuthorizedClient(
        transport, new MemberMcpTokenManager("member-1", new TokenStore(), tokenClient));

    if (variant != "fractional-year")
      await Assert.ThrowsAsync<JsonException>(
          () => authorized.GetMyFinancialProfileAsync(TestContext.Current.CancellationToken));
    else
      Assert.Equal<double?>(7.5, (await authorized.GetMyFinancialProfileAsync(
          TestContext.Current.CancellationToken)).FinancialProfile?.YearsOfCreditHistory);
  }

  [Theory]
  [InlineData("{\"success\":true,\"result\":{}}")]
  [InlineData("{\"success\":false,\"result\":{\"financial_profile\":null}}")]
  public async Task RejectsMissingOrUnsuccessfulStructuredResult(string body)
  {
    using var content = JsonDocument.Parse(body);
    using var handler = new FinancialToolHandler(content.RootElement);
    using var tokenClient = TokenClient(handler);
    using var transport = new MemberMcpClient(Origin, handler);
    var authorized = new MemberMcpAuthorizedClient(
        transport, new MemberMcpTokenManager("member-1", new TokenStore(), tokenClient));

    if (body.Contains("\"success\":false", StringComparison.Ordinal))
      await Assert.ThrowsAsync<InvalidDataException>(
          () => authorized.GetMyFinancialProfileAsync(TestContext.Current.CancellationToken));
    else
      await Assert.ThrowsAsync<JsonException>(
          () => authorized.GetMyFinancialProfileAsync(TestContext.Current.CancellationToken));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task MissingStructuredContentOrToolErrorCannotBecomeSuccessfulNull(bool toolError)
  {
    using var validNull = JsonDocument.Parse(
        "{\"success\":true,\"result\":{\"financial_profile\":null}}");
    using var handler = new FinancialToolHandler(toolError ? validNull.RootElement : null, toolError);
    using var tokenClient = TokenClient(handler);
    using var transport = new MemberMcpClient(Origin, handler);
    var authorized = new MemberMcpAuthorizedClient(
        transport, new MemberMcpTokenManager("member-1", new TokenStore(), tokenClient));

    await Assert.ThrowsAsync<InvalidDataException>(
        () => authorized.GetMyFinancialProfileAsync(TestContext.Current.CancellationToken));
  }

  private static readonly Uri Origin = new("https://example.test");

  private static MemberMcpOAuthTokenClient TokenClient(HttpMessageHandler handler) => new(
      new MemberMcpOAuthMetadata(
          "https://example.test", new Uri(Origin, "/authorize"),
          new Uri(Origin, "/token"), new Uri(Origin, "/revoke")),
      new Uri(Origin, "/api/v1/oauth/native-clients/windows"),
      new Uri(Origin, "/api/v1/mcp"), handler);

  private sealed class TokenStore : IMemberMcpOAuthTokenStore
  {
    public Task<MemberMcpOAuthTokens?> LoadAsync(MemberMcpOAuthTokenScope scope) =>
        Task.FromResult<MemberMcpOAuthTokens?>(new(
            "known-access", "known-refresh", 3600, "financial-profile:read", "Bearer"));
    public Task SaveAsync(MemberMcpOAuthTokenScope scope, MemberMcpOAuthTokens tokens) => Task.CompletedTask;
    public Task ClearAsync(MemberMcpOAuthTokenScope scope) => Task.CompletedTask;
  }

  private sealed class FinancialToolHandler(JsonElement? structuredContent, bool isError = false) : HttpMessageHandler
  {
    public string? Authorization { get; private set; }
    public string? Method { get; private set; }
    public string? ToolName { get; private set; }
    public string? Arguments { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Authorization = request.Headers.Authorization?.ToString();
      using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
      var rpc = body.RootElement;
      Method = rpc.GetProperty("method").GetString();
      ToolName = rpc.GetProperty("params").GetProperty("name").GetString();
      Arguments = rpc.GetProperty("params").GetProperty("arguments").GetRawText();
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
          jsonrpc = "2.0",
          id = rpc.GetProperty("id").GetString(),
          result = new { content = Array.Empty<object>(), structuredContent, isError }
        }), Encoding.UTF8, "application/json")
      };
    }
  }
}
