namespace Voucha.Client.Core.Navigation;

public static partial class NativeRouteCatalog
{
  private static NativeRouteCatalogEntry[] StaffEntries() =>
  [
    Included("Membership grants", NativeRouteDestinationId.MembershipGrants, "/memberships/grants", ["/memberships/grants"]),
    Included("User administration", NativeRouteDestinationId.UserAdmin, "/user/alice/admin", ["/user/:idOrUsername/admin"]),
    Included("Engineering agents", NativeRouteDestinationId.EngineeringAgents, "/agents", ["/agents", "/agent/:idOrSlug", "/agent/:idOrSlug/conversation/:conversationId"]),
    Included("Engineering queues", NativeRouteDestinationId.EngineeringQueues, "/admin/queues", ["/admin/queues"]),
    Included("Engineering PostgreSQL", NativeRouteDestinationId.EngineeringPostgresql, "/admin/postgresql", ["/admin/postgresql"]),
    Included("Engineering Valkey", NativeRouteDestinationId.EngineeringValkey, "/admin/valkey", ["/admin/valkey"]),
    Included("Engineering AI costs", NativeRouteDestinationId.EngineeringAiCosts, "/admin/ai-costs", ["/admin/ai-costs"]),
    Included("Engineering dynamic config", NativeRouteDestinationId.EngineeringDynamicConfig, "/admin/dynamic-config", ["/admin/dynamic-config"]),
    Included("Growth dashboard", NativeRouteDestinationId.GrowthDashboard, "/growth", ["/growth"]),
    Included("Moderation reports", NativeRouteDestinationId.ModerationReports, "/reports", ["/reports"]),
    Included("Moderation appeals", NativeRouteDestinationId.ModerationAppeals, "/appeals", ["/appeals"]),
    Included("Moderation disputes", NativeRouteDestinationId.ModerationDisputes, "/disputes", ["/disputes"]),
    Included("Moderation review queue", NativeRouteDestinationId.ModerationReviewQueue, "/posts/review-queue", ["/posts/review-queue"]),
    Included("Moderation admin", NativeRouteDestinationId.ModerationAdmin, "/admin/modlog", ["/admin/modlog", "/admin/moderation-analytics"]),
    Included("Moderation integrity", NativeRouteDestinationId.ModerationIntegrity, "/vote-integrity/flags", ["/vote-integrity/flags", "/vote-integrity/penalties", "/report-integrity/flags", "/report-integrity/penalties"]),
  ];
}
