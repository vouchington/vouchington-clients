using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed partial class HybridPaginationControlSourceTests
{
  [Fact]
  public void EveryRenderedPaginationControlDeclaresAContextualId()
  {
    var pages = RepoDirectory("dotnet-clients", "src", "Voucha.Client.App", "Pages");
    foreach (var path in Directory.EnumerateFiles(pages, "*.xaml", SearchOption.AllDirectories))
    {
      var controls = XDocument.Load(path).Descendants()
          .Where(element => element.Name.LocalName == "HybridPaginationControl");
      foreach (var control in controls)
      {
        Assert.Contains(
            control.Attributes(),
            attribute => attribute.Name.LocalName == "PaginationId" &&
                !string.IsNullOrWhiteSpace(attribute.Value));
      }
    }

    foreach (var path in Directory.EnumerateFiles(pages, "*.cs", SearchOption.AllDirectories))
    {
      var source = File.ReadAllText(path);
      AssertInitializersContainPaginationId(ExplicitConstruction(), source, path);
      AssertInitializersContainPaginationId(TargetTypedConstruction(), source, path);
    }
  }

  private static void AssertInitializersContainPaginationId(Regex expression, string source, string path)
  {
    foreach (Match match in expression.Matches(source))
    {
      Assert.True(
          match.Groups["body"].Value.Contains("PaginationId", StringComparison.Ordinal),
          $"HybridPaginationControl in {path} must declare PaginationId in its initializer.");
    }
  }

  private static string RepoDirectory(params string[] parts)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
      if (Directory.Exists(candidate)) return candidate;
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }

  [GeneratedRegex(@"new\s+HybridPaginationControl(?:\(\))?(?<body>\s*\{[^}]*\})?")]
  private static partial Regex ExplicitConstruction();

  [GeneratedRegex(@"HybridPaginationControl\s+\w+\s*=\s*new\(\)(?<body>\s*\{[^}]*\})?")]
  private static partial Regex TargetTypedConstruction();
}
