import VouchaLocalization

extension NativeParityCatalog {
    static let moderationGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryModerationTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryModerationSummary),
            icon: "shield.lefthalf.filled",
            entries: entries(for: .moderationReports),
            requiredRoles: ["administrator"]
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryAppealsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryAppealsSummary),
            icon: "arrow.uturn.left.circle",
            entries: entries(for: .moderationAppeals, .moderationDisputes),
            requiredRoles: ["administrator", "moderator"]
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryOperationsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryOperationsSummary),
            icon: "lock.shield",
            entries: entries(
                for:
                .moderationReviewQueue,
                .moderationAdmin,
                .moderationIntegrity
            ),
            requiredRoles: ["administrator"]
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryMyCasesTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryMyCasesSummary),
            icon: "exclamationmark.triangle",
            entries: entries(for: .moderationCases)
        )
    ]

    static let crmGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryCrmTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryCrmSummary),
            icon: "person.text.rectangle",
            entries: entries(for: .crmContacts, .membershipGrants, .supportStaffThreads, .supportStaffContacts)
        )
    ]

    static let engineeringGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryOperationsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryOperationsSummary),
            icon: "wrench.and.screwdriver",
            entries: entries(
                for: .engineeringAgents,
                .engineeringQueues,
                .engineeringPostgresql,
                .engineeringValkey,
                .engineeringAiCosts
            ),
            requiredRoles: ["administrator"]
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteMetadataStaffEngineeringDynamicConfigDynamicConfigTitle),
            summary: UiMessage(.nativeSwiftRouteMetadataStaffEngineeringDynamicConfigDynamicConfigDescription),
            icon: "slider.horizontal.3",
            entries: entries(for: .engineeringDynamicConfig)
        )
    ]

    static let growthGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryGrowthTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryGrowthSummary),
            icon: "chart.line.uptrend.xyaxis",
            entries: entries(for: .growthDashboard)
        )
    ]
}
