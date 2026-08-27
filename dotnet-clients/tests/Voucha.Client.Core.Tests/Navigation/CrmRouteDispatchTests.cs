using System.Runtime.CompilerServices;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class CrmRouteDispatchTests
{
  [Theory]
  [InlineData("/crm", CrmRouteViewKind.Contacts, null, false)]
  [InlineData("/crm/00000000-0000-7000-8000-000000000584", CrmRouteViewKind.ContactDetail, "00000000-0000-7000-8000-000000000584", false)]
  [InlineData("/support", CrmRouteViewKind.StaffSupportThreads, null, false)]
  [InlineData("/support/contacts", CrmRouteViewKind.StaffSupportContacts, null, false)]
  [InlineData("/memberships/grants", CrmRouteViewKind.MembershipGrant, null, false)]
  public void ResolveClassifiesCrmAndSiblingRoutes(
      string path,
      CrmRouteViewKind expectedKind,
      string? expectedContactId,
      bool expectedSibling)
  {
    Assert.Equal(expectedKind, CrmRouteDispatch.Resolve(path));
    Assert.Equal(expectedSibling, CrmRouteDispatch.IsSiblingRoute(path));
    Assert.Equal(expectedContactId is not null, CrmRouteDispatch.TryGetContactId(path, out var contactId));
    Assert.Equal(expectedContactId, contactId);
  }

  [Theory]
  [InlineData("/support/threads/thread-1", CrmRouteViewKind.StaffSupportThreadDetail)]
  [InlineData("/support/contacts/contact-1", CrmRouteViewKind.StaffSupportContactDetail)]
  [InlineData("/support/threads/", CrmRouteViewKind.Placeholder)]
  [InlineData("/support/threads/thread-1/extra", CrmRouteViewKind.Placeholder)]
  public void StaffSupportRoutesRequireExactlyOneNonEmptyIdentifier(string path, CrmRouteViewKind expected)
  {
    Assert.Equal(expected, CrmRouteDispatch.Resolve(path));
    Assert.Equal(expected == CrmRouteViewKind.StaffSupportThreadDetail,
        CrmRouteDispatch.TryGetStaffSupportThreadId(path, out _));
    Assert.Equal(expected == CrmRouteViewKind.StaffSupportContactDetail,
        CrmRouteDispatch.TryGetStaffSupportContactId(path, out _));
  }

  [Fact]
  public void WarmCrmNavigationReplacesTheRealizedShellContent()
  {
    var source = ReadAppSource("AppShell.CrmIntentPages.cs");
    var pageSource = ReadAppSource(Path.Combine("Pages", "MembershipGrantPage.cs"));

    Assert.Contains("content.Content as MembershipGrantPage ??", source, StringComparison.Ordinal);
    Assert.Contains("serviceProvider.GetRequiredService<MembershipGrantPage>()", source, StringComparison.Ordinal);
    Assert.Contains("ReplaceCrmContent(content, content.Content as CrmContactsPage", source, StringComparison.Ordinal);
    Assert.Contains("content.Content as CrmContactPage", source, StringComparison.Ordinal);
    Assert.Contains("ReplaceCrmContent(content, detailPage)", source, StringComparison.Ordinal);
    Assert.Contains("!ReferenceEquals(content.Content, replacement)", source, StringComparison.Ordinal);
    Assert.Contains("content.Content is IDisposable disposable", source, StringComparison.Ordinal);
    Assert.Contains("disposable.Dispose()", source, StringComparison.Ordinal);
    Assert.DoesNotContain("Unloaded += OnUnloaded", pageSource, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<StaffSupportThreadsPage>()", source, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<StaffSupportContactsPage>()", source, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<StaffSupportThreadPage>()", source, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<StaffSupportContactPage>()", source, StringComparison.Ordinal);
  }

  private static string ReadAppSource(string fileName)
  {
    foreach (var start in new[] { Path.GetDirectoryName(SourceFile())!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
      for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
      {
        var path = Path.Combine(directory.FullName, "dotnet-clients", "src", "Voucha.Client.App", fileName);
        if (File.Exists(path)) return File.ReadAllText(path);
      }
    }

    throw new DirectoryNotFoundException("Could not find the .NET app source directory.");
  }

  private static string SourceFile([CallerFilePath] string sourceFile = "") => sourceFile;
}
