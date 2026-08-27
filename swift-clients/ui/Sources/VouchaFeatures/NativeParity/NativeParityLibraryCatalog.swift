import VouchaLocalization

extension NativeParityCatalog {
    static let libraryGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryReferralsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryReferralsSummary),
            icon: "arrow.triangle.branch",
            entries: entries(for: .referrals)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryLandingPagesTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryLandingPagesSummary),
            icon: "doc.text",
            entries: entries(for: .landingPages)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryListsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryListsSummary),
            icon: "list.bullet.rectangle",
            entries: entries(for: .lists)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryBookmarksTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryBookmarksSummary),
            icon: "bookmark",
            entries: entries(for: .bookmarks)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryPlansTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryPlansSummary),
            icon: "creditcard",
            entries: entries(for: .plans)
        )
    ]
}
