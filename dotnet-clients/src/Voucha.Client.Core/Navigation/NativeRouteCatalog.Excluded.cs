namespace Voucha.Client.Core.Navigation;

public static partial class NativeRouteCatalog
{
  private static NativeRouteCatalogEntry[] ExcludedEntries() =>
  [
    Excluded("Admin namespace", "Admin surfaces are outside the non-admin native catalog.", "/admin/users", ["/admin/**"]),
    Excluded("Vote integrity", "Vote integrity review is admin-only.", "/vote-integrity/audit", ["/vote-integrity/**"]),
    Excluded("Report integrity", "Report integrity review is admin-only.", "/report-integrity/audit", ["/report-integrity/**"]),
    Excluded(
        "Referral validations",
        "Referral validation routes are administrator-only.",
        "/referral-program/123/validations",
        ["/referral-program/:id/validations", "/referral-program/:id/validations/new", "/referral-program/:id/validations/:validationId"]),
    Excluded("Unsupported topic management routes", "Topic alias search and validation routes require separate native surfaces.",
        "/topics/aliases", ["/topics/aliases", .. ExcludedTopicSettingsPatterns()]),
  ];
}
