#if DEBUG
using System.Text.Json;
using System.Text.RegularExpressions;
using Voucha.Client.Core.Tests.Support;
using Xunit;

namespace Voucha.Client.App.Tests;

// The Catalyst job bundles only the fixtures its staging script lists. The handler reads its
// fixtures at construction, so one missing from that list would crash the app at launch.
public sealed partial class CatalystAuthNewsStagedContractTests
{
  [Fact]
  public void HandlerConstructsFromExactlyTheFixturesTheStagedBundleShips()
  {
    var ids = ShippedFixtureIds();
    Assert.Contains("native.auth.email-address.login.authenticated", ids);
    using var staged = StagedRoot(ids);

    using var handler = new CatalystAuthNewsHandler(staged.Path);

    Assert.NotNull(handler);
  }

  [Fact]
  public void HandlerFailsWhenTheNativeLoginFixtureIsNotShipped()
  {
    var ids = ShippedFixtureIds().Where(id => !id.StartsWith("native.auth", StringComparison.Ordinal)).ToArray();
    using var staged = StagedRoot(ids);

    Assert.ThrowsAny<Exception>(() => new CatalystAuthNewsHandler(staged.Path));
  }

  private static string[] ShippedFixtureIds()
  {
    var script = File.ReadAllText(RepositoryFilePath.Find(
        new DirectoryInfo(AppContext.BaseDirectory), "dotnet-clients", "tooling", "catalyst-gui-auth-news.mjs"));
    var list = FixtureList().Match(script).Groups["list"].Value;
    return [.. FixtureId().Matches(list).Select(match => match.Groups["id"].Value)];
  }

  private static TempRoot StagedRoot(string[] ids)
  {
    var source = Path.Combine(Environment.GetEnvironmentVariable("VOUCHA_CONTRACT_ROOT")!, "api-fixtures", "v1");
    using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, "manifest.json")));
    var fixtures = manifest.RootElement.GetProperty("fixtures").EnumerateArray()
        .Where(item => ids.Contains(item.GetProperty("id").GetString()!)).ToArray();
    var root = new TempRoot();
    var target = Path.Combine(root.Path, "api-fixtures", "v1");
    Directory.CreateDirectory(target);
    File.WriteAllText(Path.Combine(target, "manifest.json"), JsonSerializer.Serialize(new { fixtures }));
    foreach (var fixture in fixtures)
    {
      var body = fixture.GetProperty("bodyFile").GetString()!;
      Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(target, body))!);
      File.Copy(Path.Combine(source, body), Path.Combine(target, body));
    }
    return root;
  }

  [GeneratedRegex(@"authNewsFixtures = \[(?<list>.*?)\n\]", RegexOptions.Singleline)]
  private static partial Regex FixtureList();

  [GeneratedRegex(@"\['(?<id>[^']+)',")]
  private static partial Regex FixtureId();

  private sealed class TempRoot : IDisposable
  {
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "catalyst-staged-" + Guid.NewGuid());
    public void Dispose() => Directory.Delete(Path, recursive: true);
  }
}
#endif
