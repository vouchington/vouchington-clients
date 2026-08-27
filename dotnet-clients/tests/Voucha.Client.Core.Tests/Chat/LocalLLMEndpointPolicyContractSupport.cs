using System.Text.Json;
using System.Runtime.CompilerServices;

namespace Voucha.Client.Core.Tests.Chat;

internal enum LocalLLMHostPolicyVerdict
{
  Allowed,
  Rejected,
}

internal sealed record LocalLLMHostPolicyRow(
    string Id,
    IReadOnlySet<string> RequiredConsumers,
    string Endpoint,
    LocalLLMHostPolicyVerdict Verdict,
    string Notes)
{
  public bool IsAllowed => Verdict == LocalLLMHostPolicyVerdict.Allowed;
}

internal sealed record LocalLLMOriginPair(
    string Id,
    IReadOnlySet<string> RequiredConsumers,
    string VariantA,
    string VariantB,
    string ExpectedOrigin,
    string Notes);

internal sealed class LocalLLMEndpointPolicyContract
{
  private readonly IReadOnlyList<LocalLLMHostPolicyRow> hostPolicyRows;
  private readonly IReadOnlyList<LocalLLMOriginPair> originPairs;

  private LocalLLMEndpointPolicyContract(
      IReadOnlyList<LocalLLMHostPolicyRow> hostPolicyRows,
      IReadOnlyList<LocalLLMOriginPair> originPairs) =>
      (this.hostPolicyRows, this.originPairs) = (hostPolicyRows, originPairs);

  public static LocalLLMEndpointPolicyContract Load([CallerFilePath] string sourceFile = "")
  {
    var path = FindContract(sourceFile);
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var root = document.RootElement;
    return new(
        root.GetProperty("hostPolicyRows").EnumerateArray().Select(ParseHostPolicyRow).ToArray(),
        root.GetProperty("originPairs").EnumerateArray().Select(ParseOriginPair).ToArray());
  }

  internal static IReadOnlySet<string> ParseConsumers(JsonElement value)
  {
    var consumers = value.EnumerateArray().Select(consumer => consumer.GetString()!).ToHashSet();
    foreach (var consumer in consumers)
    {
      if (consumer is not ("dotnet-core" or "swift-core" or "swift-android"))
      {
        throw new JsonException($"Unknown endpoint-policy consumer '{consumer}'.");
      }
    }
    return consumers;
  }

  internal static LocalLLMHostPolicyRow ParseHostPolicyRow(JsonElement value)
  {
    var verdictText = value.GetProperty("verdict").GetString()!;
    var verdict = verdictText switch
    {
      "allowed" => LocalLLMHostPolicyVerdict.Allowed,
      "rejected" => LocalLLMHostPolicyVerdict.Rejected,
      _ => throw new JsonException($"Unknown host-policy verdict '{verdictText}'."),
    };
    return new(
        value.GetProperty("id").GetString()!,
        ParseConsumers(value.GetProperty("requiredConsumers")),
        value.GetProperty("endpoint").GetString()!,
        verdict,
        value.GetProperty("notes").GetString()!);
  }

  internal static LocalLLMOriginPair ParseOriginPair(JsonElement value) =>
      new(
          value.GetProperty("id").GetString()!,
          ParseConsumers(value.GetProperty("requiredConsumers")),
          value.GetProperty("variantA").GetString()!,
          value.GetProperty("variantB").GetString()!,
          value.GetProperty("expectedOrigin").GetString()!,
          value.GetProperty("notes").GetString()!);

  public IReadOnlyList<LocalLLMHostPolicyRow> HostPolicyRowsFor(string consumer) =>
      hostPolicyRows.Where(row => row.RequiredConsumers.Contains(consumer)).ToArray();

  public IReadOnlyList<LocalLLMOriginPair> OriginPairsFor(string consumer) =>
      originPairs.Where(pair => pair.RequiredConsumers.Contains(consumer)).ToArray();

  private static string FindContract(string sourceFile)
  {
    foreach (var start in new[] {
        Path.GetDirectoryName(sourceFile) ?? string.Empty,
        AppContext.BaseDirectory,
        Directory.GetCurrentDirectory(),
    })
    {
      for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
      {
        var path = Path.Combine(directory.FullName, "api-fixtures", "v1", "local-llm-endpoint-policy.json");
        if (File.Exists(path)) return path;
      }
    }
    throw new FileNotFoundException("Could not locate local-llm-endpoint-policy.json.");
  }
}
