using System.Diagnostics;
using Voucha.Client.Core;
using Xunit;

namespace Voucha.Client.Core.Tests;

public sealed class TurnstileReleaseMetadataTests
{
  private const string ReleaseSiteKey = "0x4AAAAAAAAexample";

  [Fact]
  public void BuiltAssemblyMetadataFollowsTheReleaseAndDebugSiteKeyGate()
  {
    var root = Path.Combine(Path.GetTempPath(), "voucha-turnstile-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      var repoRoot = RepoRoot();
      File.Copy(Path.Combine(repoRoot, "global.json"), Path.Combine(root, "global.json"));
      var targets = Path.Combine(
          repoRoot, "dotnet-clients", "src", "Voucha.Client.App", "TurnstileSiteKey.targets");
      File.WriteAllText(Path.Combine(root, "Program.cs"), "sealed class Program { static void Main() { } }\n");
      File.WriteAllText(Path.Combine(root, "TurnstileMetadata.csproj"), $"""
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
              <AssemblyMetadata Include="VOUCHA_TURNSTILE_SITE_KEY" Value="$(VouchaTurnstileSiteKey)" />
            </ItemGroup>
          </Project>
          """);

      AssertFailure(root, "Release", null, TurnstileSiteKeys.MissingReleaseKeyMessage);
      AssertFailure(root, "Release", TurnstileSiteKeys.AlwaysPassSiteKey, TurnstileSiteKeys.RejectedTestKeyMessage);
      AssertMetadata(root, "Release", ReleaseSiteKey, ReleaseSiteKey);
      AssertMetadata(root, "Debug", null, TurnstileSiteKeys.AlwaysPassSiteKey);
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  private static void AssertFailure(string root, string configuration, string? siteKey, string message)
  {
    var result = Build(root, configuration, siteKey);
    Assert.NotEqual(0, result.ExitCode);
    Assert.Contains(message, result.Output, StringComparison.Ordinal);
  }

  private static void AssertMetadata(string root, string configuration, string? siteKey, string expected)
  {
    var result = Build(root, configuration, siteKey);
    Assert.True(result.ExitCode == 0, result.Output);
    var assemblyInfo = Directory.GetFiles(Path.Combine(root, "obj"), "*.AssemblyInfo.cs", SearchOption.AllDirectories)
        .Single(path => File.ReadAllText(path).Contains("VOUCHA_TURNSTILE_SITE_KEY", StringComparison.Ordinal));
    var text = File.ReadAllText(assemblyInfo);
    Assert.Contains("VOUCHA_TURNSTILE_SITE_KEY", text, StringComparison.Ordinal);
    Assert.Contains($"\"{expected}\"", text, StringComparison.Ordinal);
  }

  private static (int ExitCode, string Output) Build(string root, string configuration, string? siteKey)
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
    start.ArgumentList.Add("TurnstileMetadata.csproj");
    start.ArgumentList.Add("--configuration");
    start.ArgumentList.Add(configuration);
    start.ArgumentList.Add("--nologo");
    if (siteKey is not null)
    {
      start.ArgumentList.Add($"-p:VouchaTurnstileSiteKey={siteKey}");
    }

    using var process = Process.Start(start);
    Assert.NotNull(process);
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    Assert.True(process.WaitForExit(90_000));
    return (process.ExitCode, stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult());
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

    throw new InvalidOperationException("Could not find the repository root.");
  }
}
