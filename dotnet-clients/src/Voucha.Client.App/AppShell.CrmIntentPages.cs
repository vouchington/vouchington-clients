using Voucha.Client.App.Pages;
using Voucha.Client.Core.Crm;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Page CreateCrmPage(NativeRouteMatch? match, NavigationIntentViewModel intent)
  {
    var path = match?.Path ?? "/crm";
    if (CrmRouteDispatch.TryGetStaffSupportThreadId(path, out var threadId))
    {
      var page = serviceProvider.GetRequiredService<StaffSupportThreadPage>();
      page.SetContext(threadId);
      return page;
    }
    if (CrmRouteDispatch.TryGetStaffSupportContactId(path, out var supportContactId))
    {
      var page = serviceProvider.GetRequiredService<StaffSupportContactPage>();
      page.SetContext(supportContactId);
      return page;
    }

    if (CrmRouteDispatch.TryGetContactId(path, out var contactId))
    {
      var page = serviceProvider.GetRequiredService<CrmContactPage>();
      page.SetContext(contactId);
      return page;
    }

    if (CrmRouteDispatch.Resolve(path) == CrmRouteViewKind.MembershipGrant)
    {
      return serviceProvider.GetRequiredService<MembershipGrantPage>();
    }

    return CrmRouteDispatch.Resolve(path) switch
    {
      CrmRouteViewKind.StaffSupportThreads => serviceProvider.GetRequiredService<StaffSupportThreadsPage>(),
      CrmRouteViewKind.StaffSupportContacts => serviceProvider.GetRequiredService<StaffSupportContactsPage>(),
      _ => serviceProvider.GetRequiredService<CrmContactsPage>(),
    };
  }

  private async Task<bool> PrepareCrmIntentRouteMatchAsync(NativeRouteMatch match)
  {
    foreach (var content in EnumerateShellContents("crm"))
    {
      if (CrmRouteDispatch.TryGetStaffSupportThreadId(match.Path, out var threadId))
      {
        var page = serviceProvider.GetRequiredService<StaffSupportThreadPage>();
        page.SetContext(threadId);
        ReplaceCrmContent(content, page);
        await page.ApplyRouteAsync(threadId).ConfigureAwait(true);
        return false;
      }
      if (CrmRouteDispatch.TryGetStaffSupportContactId(match.Path, out var supportContactId))
      {
        var page = serviceProvider.GetRequiredService<StaffSupportContactPage>();
        page.SetContext(supportContactId);
        ReplaceCrmContent(content, page);
        await page.ApplyRouteAsync(supportContactId).ConfigureAwait(true);
        return false;
      }

      if (CrmRouteDispatch.Resolve(match.Path) == CrmRouteViewKind.MembershipGrant)
      {
        ReplaceCrmContent(content, content.Content as MembershipGrantPage ??
            serviceProvider.GetRequiredService<MembershipGrantPage>());
        return false;
      }

      if (CrmRouteDispatch.Resolve(match.Path) is CrmRouteViewKind.StaffSupportThreads or CrmRouteViewKind.StaffSupportContacts)
      {
        ReplaceCrmContent(content, CreateCrmPage(match, NavigationIntentViewModel.FromIntent(
            NavigationCatalog.All.Single(intent => intent.Id == "crm"), viewerProvider.CurrentViewer, localization)));
        return false;
      }

      if (!CrmRouteDispatch.TryGetContactId(match.Path, out var contactId))
      {
        ReplaceCrmContent(content, content.Content as CrmContactsPage ??
            serviceProvider.GetRequiredService<CrmContactsPage>());
        return false;
      }

      var detailPage = content.Content as CrmContactPage ??
          serviceProvider.GetRequiredService<CrmContactPage>();
      detailPage.SetContext(contactId);
      ReplaceCrmContent(content, detailPage);
      await detailPage.ApplyRouteAsync(contactId).ConfigureAwait(true);
      return false;
    }

    return true;
  }

  private static void ReplaceCrmContent(ShellContent content, Page replacement)
  {
    if (!ReferenceEquals(content.Content, replacement) && content.Content is IDisposable disposable)
    {
      disposable.Dispose();
    }
    content.Content = replacement;
  }
}
