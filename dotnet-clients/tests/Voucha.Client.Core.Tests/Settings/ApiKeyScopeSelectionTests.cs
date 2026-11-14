using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class ApiKeyScopeSelectionTests
{
  [Fact]
  public void ExplicitSelectionAddsTransitivePrerequisitesAndDeselectingRemovesDependants()
  {
    var selection = new ApiKeyScopeSelection([
        Scope("a:read"), Scope("b:write", "a:read"), Scope("c:write", "b:write"), Scope("private:read")]);

    selection.SetSelected("c:write", true);

    Assert.Equal(["a:read", "b:write", "c:write"], selection.SelectedScopes);
    selection.SetSelected("b:write", false);
    Assert.Equal(["a:read"], selection.SelectedScopes);
    Assert.DoesNotContain("private:read", selection.SelectedScopes);
  }

  [Fact]
  public void MissingPrerequisiteCannotPartiallySelectAnyScopes()
  {
    var selection = new ApiKeyScopeSelection([Scope("a:write", "missing:read")]);

    Assert.Throws<InvalidOperationException>(() => selection.SetSelected("a:write", true));

    Assert.Empty(selection.SelectedScopes);
  }

  [Fact]
  public void CyclicPrerequisitesFailClosed()
  {
    var selection = new ApiKeyScopeSelection([Scope("a:read", "b:read"), Scope("b:read", "a:read")]);

    Assert.Throws<InvalidOperationException>(() => selection.SetSelected("a:read", true));

    Assert.Empty(selection.SelectedScopes);
  }

  [Fact]
  public void ResettingTheCatalogueClearsAllSelection()
  {
    var selection = new ApiKeyScopeSelection([Scope("a:read")]);
    selection.SetSelected("a:read", true);

    selection.Replace([Scope("other:read")]);

    Assert.Empty(selection.SelectedScopes);
    Assert.Equal("other:read", Assert.Single(selection.Scopes).Scope);
  }

  [Theory]
  [InlineData("community_moderation")]
  [InlineData("brand")]
  public void DifferentAudiencesCannotMixOrSupplyPrerequisites(string audience)
  {
    var selection = new ApiKeyScopeSelection([
        Scope("member:read"),
        new("separate:read", audience, "fixture", "read", ["api-key"], null, null),
        new("invalid:write", audience, "fixture", "write", ["api-key"], null, "member:read"),
    ]);
    selection.SetSelected("member:read", true);
    Assert.Throws<InvalidOperationException>(() => selection.SetSelected("separate:read", true));
    Assert.Equal(["member:read"], selection.SelectedScopes);
    selection.SetSelected("member:read", false);
    Assert.Throws<InvalidOperationException>(() => selection.SetSelected("invalid:write", true));
    Assert.Empty(selection.SelectedScopes);
    selection.SetSelected("separate:read", true);
    Assert.Equal(["separate:read"], selection.SelectedScopes);
  }

  private static ScopeCatalogEntry Scope(string name, string? requires = null) =>
      new(name, "user", "resource", "read", ["api-key"], null, requires);
}
