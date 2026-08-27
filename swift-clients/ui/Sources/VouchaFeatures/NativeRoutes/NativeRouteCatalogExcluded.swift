enum NativeRouteCatalogExcluded {
    static let entries: [NativeRouteCatalogEntry] = [
        .excluded(
            auditFamily: "Admin namespace",
            auditReason: "Admin surfaces are outside the non-admin native catalog.",
            representativePath: "/admin/users",
            patterns: ["/admin/**"]
        ),
        .excluded(
            auditFamily: "Vote integrity",
            auditReason: "Vote integrity review is admin-only.",
            representativePath: "/vote-integrity/audit",
            patterns: ["/vote-integrity/**"]
        ),
        .excluded(
            auditFamily: "Report integrity",
            auditReason: "Report integrity review is admin-only.",
            representativePath: "/report-integrity/audit",
            patterns: ["/report-integrity/**"]
        ),
        .excluded(
            auditFamily: "Referral validations",
            auditReason: "Referral validation routes are administrator-only.",
            representativePath: "/referral-program/123/validations",
            patterns: [
                "/referral-program/:id/validations",
                "/referral-program/:id/validations/new",
                "/referral-program/:id/validations/:validationId"
            ]
        ),
        .excluded(
            auditFamily: "Unsupported topic management routes",
            auditReason: "Topic alias search and validation routes require separate native surfaces.",
            representativePath: "/topics/aliases",
            patterns: ["/topics/aliases"] + NativeRouteCatalogPatterns.excludedTopicSettingsPatterns
        )
    ]
}
