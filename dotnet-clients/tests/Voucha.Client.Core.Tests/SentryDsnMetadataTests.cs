using System.Diagnostics;
using Xunit;

namespace Voucha.Client.Core.Tests;

public sealed class SentryDsnMetadataTests
{
  [Fact]
  public void ReleaseBuildKeepsAnEmptyDsnAndDoesNotFail()
  {
    var root = Path.Combine(Path.GetTempPath(), "voucha-sentry-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      File.Copy(Path.Combine(RepoRoot(), "global.json"), Path.Combine(root, "global.json"));
      var targets = Path.Combine(RepoRoot(), "dotnet-clients", "src", "Voucha.Client.App", "SentryDsn.targets");
      File.WriteAllText(Path.Combine(root, "Program.cs"), "sealed class Program { static void Main() { } }\n");
      File.WriteAllText(Path.Combine(root, "SentryMetadata.csproj"), $"""
          <Project Sdk="Microsoft.NET.Sdk">
            <PropertyGroup>
              <TargetFramework>net10.0</TargetFramework>
              <OutputType>Exe</OutputType>
              <ImplicitUsings>disable</ImplicitUsings>
              <Nullable>disable</Nullable>
              <AnalysisMode>None</AnalysisMode>
              <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
            </PropertyGroup>
            <ItemGroup>
              <Compile Include="Program.cs" />
            </ItemGroup>
            <Import Project="{targets}" />
            <ItemGroup>
              <AssemblyMetadata Include="VOUCHA_SENTRY_DSN" Value="$(VouchaSentryDsn)" />
            </ItemGroup>
          </Project>
          """);

      AssertMetadata(root, "Release", environmentDsn: null, propertyDsn: null, "");
      AssertMetadata(root, "Debug", environmentDsn: "  ", propertyDsn: null, "");
      AssertMetadata(root, "Release", environmentDsn: null, propertyDsn: "https://public@example.invalid/1", "https://public@example.invalid/1");
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  private static void AssertMetadata(
      string root,
      string configuration,
      string? environmentDsn,
      string? propertyDsn,
      string expected)
  {
    foreach (var folder in new[] { "bin", "obj" })
    {
      var path = Path.Combine(root, folder);
      if (Directory.Exists(path))
      {
        Directory.Delete(path, recursive: true);
      }
    }

    var start = new ProcessStartInfo("dotnet")
    {
      WorkingDirectory = root,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
    };
    start.ArgumentList.Add("build");
    start.ArgumentList.Add("SentryMetadata.csproj");
    start.ArgumentList.Add("--configuration");
    start.ArgumentList.Add(configuration);
    start.ArgumentList.Add("--nologo");
    start.Environment["UseArtifactsOutput"] = "false";
    if (environmentDsn is null)
    {
      start.Environment.Remove("VOUCHA_SENTRY_DSN");
    }
    else
    {
      start.Environment["VOUCHA_SENTRY_DSN"] = environmentDsn;
    }

    if (propertyDsn is not null)
    {
      start.ArgumentList.Add($"-p:VouchaSentryDsn={propertyDsn}");
    }

    using var process = Process.Start(start);
    Assert.NotNull(process);
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    Assert.True(process.WaitForExit(90_000));
    Assert.True(process.ExitCode == 0, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    var assemblyInfo = Directory.GetFiles(Path.Combine(root, "obj"), "*.AssemblyInfo.cs", SearchOption.AllDirectories)
        .Single(path => File.ReadAllText(path).Contains("VOUCHA_SENTRY_DSN", StringComparison.Ordinal));
    var line = File.ReadAllLines(assemblyInfo).Single(candidate =>
        candidate.Contains("VOUCHA_SENTRY_DSN", StringComparison.Ordinal));
    var match = System.Text.RegularExpressions.Regex.Match(line, "VOUCHA_SENTRY_DSN\",\\s*\"(?<value>[^\"]*)\"");
    Assert.True(match.Success, line);
    Assert.Equal(expected, match.Groups["value"].Value);
  }

  private static string RepoRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      if (File.Exists(Path.Combine(directory.FullName, "pnpm-workspace.yaml")))
      {
        return directory.FullName;
      }

      directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root was not found.");
  }
}
