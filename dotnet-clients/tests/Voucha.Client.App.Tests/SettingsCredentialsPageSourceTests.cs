using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class SettingsCredentialsPageSourceTests
{
  [Fact]
  public void ApiKeyControlsBindCatalogueRowsAndValidatedCreationState()
  {
    var document = SettingsDocument();
    var scopeList = ElementWithBinding(document, "BindableLayout.ItemsSource", "{Binding ApiKeyScopes}");
    var scopeTemplate = Assert.Single(scopeList.Descendants(), element => element.Name.LocalName == "DataTemplate");
    var checkBox = Assert.Single(scopeTemplate.Descendants(), element => element.Name.LocalName == "CheckBox");
    Assert.Equal("{Binding IsSelected, Mode=OneWay}", Attribute(checkBox, "IsChecked"));
    Assert.Equal("OnApiKeyScopeChanged", Attribute(checkBox, "CheckedChanged"));
    Assert.Contains(scopeTemplate.Descendants(), element => Attribute(element, "Text") == "{Binding Audience}");
    Assert.Contains(scopeTemplate.Descendants(), element => Attribute(element, "Text") == "{Binding Requires}");
    Assert.Contains(scopeTemplate.Descendants(), element => Attribute(element, "Text") == "{Binding Description}");
    foreach (var key in new[] { "resource", "action", "audience" })
      Assert.Contains(scopeTemplate.Descendants(), element =>
          Attribute(element, "Text") == $"{{DynamicResource native.credentials.{key}}}");
    var create = document.Descendants().Single(element =>
        Attribute(element, "Clicked") == "OnCreateApiKeyClicked");
    Assert.Equal("{Binding CanCreateApiKey}", Attribute(create, "IsEnabled"));
  }

  [Fact]
  public void ConnectedAppsRenderGrantIdentityMetadataRevocationAndPagination()
  {
    var document = SettingsDocument();
    var list = ElementWithBinding(document, "BindableLayout.ItemsSource", "{Binding LocalizedOAuthGrants}");
    var template = Assert.Single(list.Descendants(), element => element.Name.LocalName == "DataTemplate");
    foreach (var binding in new[] { "ClientName", "Verification", "Resource", "Scopes", "Activity" })
      Assert.Contains(template.Descendants(), element => Attribute(element, "Text") == $"{{Binding {binding}}}");
    var revoke = template.Descendants().Single(element =>
        Attribute(element, "Clicked") == "OnRevokeOAuthGrantClicked");
    Assert.Equal("{Binding .}", Attribute(revoke, "CommandParameter"));
    var pagination = document.Descendants().Single(element =>
        Attribute(element, "PaginationId") == "settings-oauth-grants");
    Assert.Equal("{Binding HasOAuthGrantPaginationError}", Attribute(pagination, "HasError"));
    Assert.Equal("OnLoadMoreOAuthGrantsRequested", Attribute(pagination, "LoadNextPageRequested"));
  }

  private static XElement ElementWithBinding(XDocument document, string attribute, string value) =>
      document.Descendants().Single(element => Attribute(element, attribute) == value);

  private static string? Attribute(XElement element, string name) =>
      element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == name)?.Value;

  private static XDocument SettingsDocument([CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return XDocument.Load(Path.Combine(
        root?.FullName ?? throw new DirectoryNotFoundException(),
        "src", "Voucha.Client.App", "Pages", "SettingsPage.xaml"));
  }
}
