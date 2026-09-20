using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Navigation;

public static partial class NavigationCatalog
{
  private static NavIntent ModerationIntent { get; } = new(
      "moderation",
      UiMessageKey.ExtractedIntentsAdminModeration126d4415,
      "shield-alert",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsAdminReportsDacca3cb,
            "sidebar-group-admin-report-triage",
            [
              new NavItem(UiMessageKey.ExtractedIntentsAdminReportsDacca3cb, "/reports", "sidebar-link-reports"),
            ],
            Roles: ["administrator"]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsAdminAppeals03e8c5a5,
            "sidebar-group-staff-review",
            [
              new NavItem(UiMessageKey.ExtractedIntentsAdminAppeals03e8c5a5, "/appeals", "sidebar-link-appeals"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminReviewDisputes25c25858, "/disputes", "sidebar-link-review-disputes"),
            ],
            Roles: ["administrator", "moderator"]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsAdminModeration126d4415,
            "sidebar-group-admin-moderation",
            [
              new NavItem(UiMessageKey.ExtractedIntentsAdminReviewQueue83c3c922, "/posts/review-queue", "sidebar-link-review-queue"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminModLog7d4b507b, "/admin/modlog", "sidebar-link-mod-log"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminModAnalytics0a733791, "/admin/moderation-analytics", "sidebar-link-mod-analytics"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminVoteIntegrityC05b33b5, "/vote-integrity/flags", "sidebar-link-vote-integrity"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminReportIntegrity7d7661b2, "/report-integrity/flags", "sidebar-link-report-integrity"),
            ],
            Roles: ["administrator"]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsAdminMyCasesD27ecf6f,
            "sidebar-group-my-moderation",
            [
              new NavItem(UiMessageKey.ExtractedIntentsAdminMyAppeals1e44b863, "/my/appeals", "sidebar-nav-my-appeals", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsAdminMyDisputesC988b30a, "/my/disputes", "sidebar-nav-my-disputes", RequiresAuth: true),
              new NavItem(UiMessageKey.NativeSwiftCommunityRowsModerationTransparency, "/moderation-transparency", "sidebar-nav-moderation-transparency", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ],
      RequiresAuth: true);

  private static NavIntent CrmIntent { get; } = new(
      "crm",
      UiMessageKey.ExtractedIntentsAdminCrm130f70ae,
      "contact",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsAdminCrm130f70ae,
            "sidebar-group-admin-crm",
            [
              new NavItem(UiMessageKey.ExtractedIntentsAdminCrm130f70ae, "/crm", "sidebar-link-crm"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminMembershipsB50f1a42, "/memberships/grants", "sidebar-link-memberships"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminSupportBe91940b, "/support", "sidebar-link-support"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminSupportContacts96f650a3, "/support/contacts", "sidebar-link-support-contacts"),
            ]),
      ],
      Roles: ["administrator"]);

  private static NavIntent EngineeringIntent { get; } = new(
      "engineering",
      UiMessageKey.ExtractedIntentsAdminEngineering729bb48d,
      "settings-2",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsAdminEngineering729bb48d,
            "sidebar-group-admin-engineering",
            [
              new NavItem(UiMessageKey.ExtractedIntentsAdminQueuesBe77db11, "/admin/queues", "sidebar-link-queues", Exact: true),
              new NavItem(UiMessageKey.ExtractedIntentsAdminPostgresqlCc52d032, "/admin/postgresql", "sidebar-link-postgresql"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminValkey2392ad6b, "/admin/valkey", "sidebar-link-valkey"),
              new NavItem(UiMessageKey.ExtractedIntentsAdminAiCosts75cce222, "/admin/ai-costs", "sidebar-link-ai-costs"),
            ],
            Roles: ["administrator"]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsAdminDynamicConfig59cf5829,
            "sidebar-group-dynamic-config",
            [new NavItem(UiMessageKey.ExtractedIntentsAdminDynamicConfig59cf5829, "/admin/dynamic-config", "sidebar-link-admin-dynamic-config")]),
      ],
      Roles: ["administrator", "moderator", "developer", "customer_support", "investor"]);

  private static NavIntent GrowthIntent { get; } = new(
      "growth",
      UiMessageKey.ExtractedIntentsAdminGrowth66b06e99,
      "trending-up",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsAdminGrowth66b06e99,
            "sidebar-group-admin-growth",
            [
              new NavItem(UiMessageKey.ExtractedIntentsAdminGrowth66b06e99, "/growth", "sidebar-link-growth"),
            ]),
      ],
      Roles: ["administrator", "investor"]);
}
