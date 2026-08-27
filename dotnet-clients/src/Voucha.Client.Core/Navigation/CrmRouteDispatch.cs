using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.Navigation;

public enum CrmRouteViewKind
{
  Contacts,
  ContactDetail,
  MembershipGrant,
  StaffSupportThreads,
  StaffSupportThreadDetail,
  StaffSupportContacts,
  StaffSupportContactDetail,
  Placeholder,
}

public static class CrmRouteDispatch
{
  public static CrmRouteViewKind Resolve(string path)
  {
    ArgumentNullException.ThrowIfNull(path);
    return TryGetStaffSupportThreadId(path, out _) ? CrmRouteViewKind.StaffSupportThreadDetail :
        TryGetStaffSupportContactId(path, out _) ? CrmRouteViewKind.StaffSupportContactDetail :
        TryGetContactId(path, out _) ? CrmRouteViewKind.ContactDetail :
        path == "/support/contacts" ? CrmRouteViewKind.StaffSupportContacts :
        path == "/support" ? CrmRouteViewKind.StaffSupportThreads :
        path == "/memberships/grants" ? CrmRouteViewKind.MembershipGrant :
        path == "/crm" ? CrmRouteViewKind.Contacts :
        CrmRouteViewKind.Placeholder;
  }

  public static bool TryGetStaffSupportThreadId(string path, [NotNullWhen(true)] out string? threadId) =>
      TryGetStaffId(path, "/support/threads/", "threads", out threadId);

  public static bool TryGetStaffSupportContactId(string path, [NotNullWhen(true)] out string? contactId) =>
      TryGetStaffId(path, "/support/contacts/", "contacts", out contactId);

  private static bool TryGetStaffId(string path, string prefix, string segment, [NotNullWhen(true)] out string? id)
  {
    ArgumentNullException.ThrowIfNull(path);
    id = null;
    if (!path.StartsWith(prefix, StringComparison.Ordinal)) return false;
    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (segments.Length != 3 || segments[0] != "support" || segments[1] != segment) return false;
    id = segments[2];
    return id.Length > 0;
  }

  public static bool TryGetContactId(string path, [NotNullWhen(true)] out string? contactId)
  {
    ArgumentNullException.ThrowIfNull(path);
    contactId = null;
    if (!path.StartsWith("/crm/", StringComparison.Ordinal))
    {
      return false;
    }

    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (segments.Length != 2 || !string.Equals(segments[0], "crm", StringComparison.Ordinal))
    {
      return false;
    }

    contactId = segments[1];
    return contactId.Length > 0;
  }

  public static bool IsSiblingRoute(string path)
  {
    ArgumentNullException.ThrowIfNull(path);
    return false;
  }
}
