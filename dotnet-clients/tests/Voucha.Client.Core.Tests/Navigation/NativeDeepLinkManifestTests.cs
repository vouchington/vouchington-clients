using System.Xml.Linq;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NativeDeepLinkManifestTests
{
  [Fact]
  public void WindowsManifestRegistersVouchaProtocol()
  {
    XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
    var document = XDocument.Load(RepoPath(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "Platforms",
        "Windows",
        "Package.appxmanifest"));

    var protocol = document
        .Descendants(uap + "Protocol")
        .SingleOrDefault(element => element.Attribute("Name")?.Value == "voucha");

    Assert.NotNull(protocol);
  }

  [Fact]
  public void WindowsAppRedirectsProtocolActivationsToPrimaryInstance()
  {
    var source = File.ReadAllText(RepoPath(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "Platforms",
        "Windows",
        "App.xaml.cs"));

    Assert.Contains("AppInstance.FindOrRegisterForKey", source, StringComparison.Ordinal);
    Assert.Contains("RedirectActivationToAsync", source, StringComparison.Ordinal);
    Assert.Contains("Process.GetCurrentProcess().Kill()", source, StringComparison.Ordinal);
  }

  [Fact]
  public void MacCatalystInfoPlistRegistersVouchaProtocol()
  {
    var plist = File.ReadAllText(RepoPath(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "Platforms",
        "MacCatalyst",
        "Info.plist"));

    Assert.Contains("<key>CFBundleURLSchemes</key>", plist, StringComparison.Ordinal);
    Assert.Contains("<string>voucha</string>", plist, StringComparison.Ordinal);
  }

  [Fact]
  public void LocalModelPlatformManifestsDeclareRequiredNetworkingAndWindowsCapability()
  {
    var macPlist = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Platforms", "MacCatalyst", "Info.plist"));
    var windowsManifest = XDocument.Load(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Platforms", "Windows", "Package.appxmanifest"));
    XNamespace foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

    Assert.Contains("<key>NSAllowsLocalNetworking</key>", macPlist, StringComparison.Ordinal);
    Assert.Contains("<true />", macPlist, StringComparison.Ordinal);
    Assert.Contains("<key>NSLocalNetworkUsageDescription</key>", macPlist, StringComparison.Ordinal);
    Assert.Contains("<string>Voucha connects to language models on your local network.</string>", macPlist, StringComparison.Ordinal);
    Assert.Equal("10.0.17763.0", windowsManifest.Descendants(foundation + "TargetDeviceFamily").Single().Attribute("MinVersion")?.Value);
    Assert.Contains(windowsManifest.Descendants(rescap + "Capability"), capability => capability.Attribute("Name")?.Value == "systemAIModels");
  }

  [Fact]
  public void WindowsProjectPinsTheSystemLanguageModelSdkWithoutRaisingTheAppWidePlatformFloor()
  {
    var project = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Voucha.Client.App.csproj"));
    var packages = File.ReadAllText(RepoPath("dotnet-clients", "Directory.Packages.props"));
    Assert.Contains("net10.0-windows10.0.26100.0", project, StringComparison.Ordinal);
    Assert.Contains("<SupportedOSPlatformVersion Condition=\"$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'\">10.0.17763.0</SupportedOSPlatformVersion>", project, StringComparison.Ordinal);
    Assert.Contains("<TargetPlatformMinVersion Condition=\"$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'\">10.0.17763.0</TargetPlatformMinVersion>", project, StringComparison.Ordinal);
    Assert.Contains("Microsoft.WindowsAppSDK\" Version=\"1.8.260710003", packages, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("en", "Voucha connects to language models on your local network.")]
  [InlineData("es", "Voucha se conecta a modelos de lenguaje en tu red local.")]
  [InlineData("fr", "Voucha se connecte à des modèles de langage sur votre réseau local.")]
  [InlineData("pt", "Voucha conecta-se a modelos de linguagem na sua rede local.")]
  public void MacCatalystLocalNetworkPurposeIsLocalized(string locale, string description)
  {
    var project = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Voucha.Client.App.csproj"));
    var relativePath = $"Platforms/MacCatalyst/{locale}.lproj/InfoPlist.strings";
    var strings = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Platforms", "MacCatalyst", $"{locale}.lproj", "InfoPlist.strings"));

    Assert.Contains($"<BundleResource Include=\"{relativePath}\" LogicalName=\"{locale}.lproj/InfoPlist.strings\" />", project, StringComparison.Ordinal);
    Assert.Contains($"\"NSLocalNetworkUsageDescription\" = \"{description}\";", strings, StringComparison.Ordinal);
  }

  private static string RepoPath(params string[] parts)
  {
    var starts = new[]
    {
      Environment.GetEnvironmentVariable("PWD"),
      Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"),
      Directory.GetCurrentDirectory(),
      Uri.UnescapeDataString(AppContext.BaseDirectory),
    }.Where(start => !string.IsNullOrWhiteSpace(start));
    foreach (var start in starts)
    {
      for (var directory = new DirectoryInfo(start!); directory is not null; directory = directory.Parent)
      {
        var candidate = Path.Combine([directory.FullName, .. parts]);
        if (File.Exists(candidate)) return candidate;
      }
    }
    throw new DirectoryNotFoundException("Could not find the repository root.");
  }
}
