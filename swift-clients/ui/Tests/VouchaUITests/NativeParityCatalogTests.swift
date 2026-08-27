import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeParityCatalogTests: XCTestCase {
    private let turnstileSiteKey = "test-site-key"

    func testNativeParitySectionsExposeGroupedRouteFamilies() {
        XCTAssertEqual(AppSection.discover.nativeParityGroups.map { uiEnglish($0.title) }, ["Discovery", "People"])
        XCTAssertEqual(
            AppSection.topics.nativeParityGroups.map { uiEnglish($0.title) },
            ["Topics", "Topic administration", "Sources", "Domains"]
        )
        XCTAssertEqual(AppSection.communities.nativeParityGroups.map { uiEnglish($0.title) }, ["Communities"])
        XCTAssertEqual(
            AppSection.messages.nativeParityGroups.map { uiEnglish($0.title) },
            ["Messages", "Chat", "Notifications"]
        )
        XCTAssertEqual(
            AppSection.settings.nativeParityGroups.map { uiEnglish($0.title) },
            ["Account", "Profile", "Advanced"]
        )
        XCTAssertEqual(
            AppSection.library.nativeParityGroups.map { uiEnglish($0.title) },
            ["Referrals", "Landing Pages", "Lists", "Bookmarks", "Plans"]
        )
        XCTAssertEqual(
            AppSection.actions.nativeParityGroups.map { uiEnglish($0.title) },
            ["Appeals", "Recommendations", "Compare", "Create & edit"]
        )
        XCTAssertEqual(
            AppSection.moderation.nativeParityGroups.map { uiEnglish($0.title) },
            ["Moderation", "Appeals", "Operations", "My Cases"]
        )
        XCTAssertEqual(AppSection.crm.nativeParityGroups.map { uiEnglish($0.title) }, ["CRM"])
        XCTAssertEqual(
            AppSection.engineering.nativeParityGroups.map { uiEnglish($0.title) },
            ["Operations", "Dynamic config"]
        )
        XCTAssertEqual(AppSection.growth.nativeParityGroups.map { uiEnglish($0.title) }, ["Growth"])
    }

    func testNativeParityGroupsReferenceExpectedDestinationCounts() {
        XCTAssertEqual(AppSection.discover.nativeParityGroups.first?.entries.count, 4)
        XCTAssertEqual(AppSection.discover.nativeParityGroups[1].entries.count, 2)
        XCTAssertEqual(AppSection.topics.nativeParityGroups[0].entries.count, 3)
        XCTAssertEqual(AppSection.topics.nativeParityGroups[1].entries.count, 1)
        XCTAssertEqual(AppSection.topics.nativeParityGroups[2].entries.count, 3)
        XCTAssertEqual(AppSection.topics.nativeParityGroups[3].entries.count, 4)
        XCTAssertEqual(AppSection.messages.nativeParityGroups[1].entries.count, 2)
        XCTAssertEqual(AppSection.settings.nativeParityGroups[2].entries.count, 3)
        XCTAssertEqual(AppSection.actions.nativeParityGroups[3].entries.count, 2)
        XCTAssertEqual(AppSection.moderation.nativeParityGroups[0].entries.count, 1)
        XCTAssertEqual(AppSection.moderation.nativeParityGroups[1].entries.count, 2)
        XCTAssertEqual(AppSection.moderation.nativeParityGroups[2].entries.count, 3)
        XCTAssertEqual(AppSection.moderation.nativeParityGroups[3].entries.count, 1)
    }

    func testNativeParityGroupsReferenceExpectedDestinations() {
        XCTAssertEqual(
            destinations(in: AppSection.discover),
            [.webSearch, .feedReferralLinks, .postsBrowse, .storiesBrowse, .usersBrowse, .userProfile]
        )
        XCTAssertEqual(
            destinations(in: AppSection.topics),
            [
                .topicsBrowse,
                .topicImportExport,
                .topicDetail,
                .topicManagement,
                .sourcesBrowse,
                .sourceImportExport,
                .sourceDetail,
                .domainsBrowse,
                .domainDetail,
                .urlsBrowse,
                .urlDetail
            ]
        )
        XCTAssertEqual(destinations(in: AppSection.communities), [.communitiesBrowse, .communityDetail])
        XCTAssertEqual(destinations(in: AppSection.messages), [.messages, .chat, .support, .notifications])
        XCTAssertEqual(
            destinations(in: AppSection.settings),
            [
                .accountSettings, .profileSettings, .household, .paymentCards, .pointValuations,
                .spendingCategories, .rewardsProgramStatuses,
                .notificationSettings, .advancedSettings, .landingPages
            ]
        )
        XCTAssertEqual(destinations(in: AppSection.library), [.referrals, .landingPages, .lists, .bookmarks, .plans])
        XCTAssertEqual(
            destinations(in: AppSection.actions),
            [.moderationCases, .moderationTransparency, .topicRecommendations, .compare, .postDetail, .postCompose]
        )
        XCTAssertEqual(
            destinations(in: AppSection.moderation),
            [
                .moderationReports,
                .moderationAppeals,
                .moderationDisputes,
                .moderationReviewQueue,
                .moderationAdmin,
                .moderationIntegrity,
                .moderationCases
            ]
        )
        XCTAssertEqual(
            destinations(in: AppSection.crm),
            [.crmContacts, .membershipGrants, .supportStaffThreads, .supportStaffContacts]
        )
        XCTAssertEqual(
            destinations(in: AppSection.engineering),
            [
                .engineeringAgents,
                .engineeringQueues,
                .engineeringPostgresql,
                .engineeringValkey,
                .engineeringAiCosts,
                .engineeringDynamicConfig
            ]
        )
        XCTAssertEqual(destinations(in: AppSection.growth), [.growthDashboard])
    }

    func testNativeParitySectionsAreTheOnlySectionsWithRouteGroups() {
        let sectionsWithGroups = Set(AppSection.allCases.filter { !$0.nativeParityGroups.isEmpty })
        XCTAssertEqual(
            sectionsWithGroups,
            [
                .discover,
                .topics,
                .communities,
                .messages,
                .settings,
                .library,
                .actions,
                .moderation,
                .crm,
                .engineering,
                .growth
            ]
        )
    }

    func testNativeParityGroupsExposeExpectedEntryCounts() {
        let expectations: [(AppSection, [Int])] = [
            (.discover, [4, 2]),
            (.topics, [3, 1, 3, 4]),
            (.communities, [2]),
            (.messages, [1, 2, 1]),
            (.settings, [1, 6, 3]),
            (.library, [1, 1, 1, 1, 1]),
            (.actions, [2, 1, 1, 2]),
            (.moderation, [1, 2, 3, 1]),
            (.crm, [4]),
            (.engineering, [5, 1]),
            (.growth, [1])
        ]

        for (section, counts) in expectations {
            XCTAssertEqual(
                section.nativeParityGroups.map(\.entries.count),
                counts,
                "Unexpected native parity entry counts for \(section.rawValue)"
            )
        }
    }

    func testDirectoryViewInitializerStoresGroups() {
        let groups = AppSection.actions.nativeParityGroups
        let sut = NativeRouteFamilyDirectoryView(groups: groups)

        XCTAssertEqual(sut.groups, groups)
    }

    func testModerationNativeParityGroupsFilterStaffRoutesByRole() {
        XCTAssertEqual(
            filteredDestinations(in: .moderation, isSignedIn: true, userRoles: []),
            [.moderationCases]
        )
        XCTAssertEqual(
            filteredDestinations(in: .moderation, isSignedIn: false, userRoles: ["administrator"]),
            []
        )
        XCTAssertEqual(
            filteredDestinations(in: .moderation, isSignedIn: true, userRoles: ["administrator"]),
            [
                .moderationReports,
                .moderationAppeals,
                .moderationDisputes,
                .moderationReviewQueue,
                .moderationAdmin,
                .moderationIntegrity,
                .moderationCases
            ]
        )
        XCTAssertEqual(
            filteredDestinations(in: .moderation, isSignedIn: true, userRoles: ["moderator"]),
            [.moderationAppeals, .moderationDisputes, .moderationCases]
        )
    }

    func testActionNativeParityGroupsKeepGenericComposeVisibleByRole() {
        XCTAssertEqual(
            filteredDestinations(in: .actions, isSignedIn: true, userRoles: []),
            [.moderationCases, .moderationTransparency, .topicRecommendations, .compare, .postDetail, .postCompose]
        )
        XCTAssertEqual(
            filteredDestinations(in: .actions, isSignedIn: true, userRoles: ["administrator"]),
            [.moderationCases, .moderationTransparency, .topicRecommendations, .compare, .postDetail, .postCompose]
        )
    }

    func testFediverseNativeParityEntryRequiresFeatureFlag() {
        XCTAssertFalse(destinations(in: .discover).contains(.fediverseSearch))
        XCTAssertFalse(destinations(in: .discover).contains(.fediverseInstances))
        XCTAssertEqual(
            destinations(in: .discover, featureFlags: ["fediverse": true]),
            [
                .webSearch,
                .fediverseSearch,
                .fediverseInstances,
                .feedReferralLinks,
                .postsBrowse,
                .storiesBrowse,
                .usersBrowse,
                .userProfile
            ]
        )
    }

    func testDirectoryViewRendersEmptyState() throws {
        let sut = NativeRouteFamilyDirectoryView(groups: [])

        XCTAssertEqual(try sut.inspect().find(text: "No native destinations").string(), "No native destinations")
        XCTAssertEqual(
            try sut.inspect().find(text: "This section does not have native route families yet.").string(),
            "This section does not have native route families yet."
        )
    }

    func testDirectoryViewRendersNativeRouteFamilies() throws {
        let sut = NativeRouteFamilyDirectoryView(groups: AppSection.discover.nativeParityGroups)

        XCTAssertEqual(try sut.inspect().find(text: "Discovery").string(), "Discovery")
        XCTAssertEqual(try sut.inspect().find(text: "People").string(), "People")
        XCTAssertEqual(
            try sut.inspect().find(text: "Browse broad entry points that surface content worth opening next.").string(),
            "Browse broad entry points that surface content worth opening next."
        )
        XCTAssertEqual(try sut.inspect().find(text: "4 routes").string(), "4 routes")
        XCTAssertEqual(try sut.inspect().find(text: "2 routes").string(), "2 routes")
    }

    func testDetailViewRendersDestinationEntriesAndPatterns() throws {
        let group = AppSection.actions.nativeParityGroups[3]
        let sut = NativeRouteFamilyDetailView(group: group, turnstileSiteKey: turnstileSiteKey)

        XCTAssertEqual(try sut.inspect().find(text: "Create & edit").string(), "Create & edit")
        XCTAssertEqual(
            try sut.inspect().find(text: "Launch native post compose and detail edit flows.").string(),
            "Launch native post compose and detail edit flows."
        )
        XCTAssertEqual(try sut.inspect().find(text: "Post detail").string(), "Post detail")
        XCTAssertEqual(try sut.inspect().find(text: "Compose").string(), "Compose")
        XCTAssertEqual(try sut.inspect().find(text: "post-detail").string(), "post-detail")
        XCTAssertEqual(try sut.inspect().find(text: "/review/123").string(), "/review/123")
    }

    func testSettingsDirectoryRendersLocalizedNotificationsEntryForItsCanonicalRoute() throws {
        let group = AppSection.settings.nativeParityGroups[2]
        let sut = NativeRouteFamilyDetailView(group: group, turnstileSiteKey: turnstileSiteKey)

        XCTAssertEqual(try sut.inspect().find(text: "Notifications").string(), "Notifications")
        XCTAssertEqual(
            try sut.inspect().find(text: "/my/notification-settings").string(),
            "/my/notification-settings"
        )
        XCTAssertEqual(group.entries.first?.destinationIdentifier, .notificationSettings)
    }

    func testEveryIncludedDestinationIdentifierHasNativeMetadata() throws {
        for entry in NativeRouteCatalog.includedEntries {
            let destination = try XCTUnwrap(entry.destinationIdentifier)

            XCTAssertFalse(destination.rawValueIcon.isEmpty, "Missing icon for \(destination.rawValue)")
            XCTAssertFalse(destination.nativeRows.isEmpty, "Missing native rows for \(destination.rawValue)")
            XCTAssertTrue(
                destination.nativeRows.allSatisfy { !$0.icon.isEmpty && !$0.title.isEmpty && !$0.detail.isEmpty },
                "Incomplete native rows for \(destination.rawValue)"
            )
            XCTAssertEqual(destination.nativeRows.count, Set(destination.nativeRows.map(\.id)).count)
        }
    }

    func testDestinationViewRendersSearchSurface() throws {
        let entry = try entry(for: .webSearch)
        let sut = NativeRouteDestinationView(entry: entry)

        XCTAssertEqual(try sut.inspect().find(text: "Search results").string(), "Search results")
        XCTAssertEqual(
            try sut.inspect().find(text: "Search across Voucha content and people").string(),
            "Search across Voucha content and people"
        )
        XCTAssertEqual(try sut.inspect().find(text: "Search results").string(), "Search results")
    }

    func testDestinationViewRendersPostComposeSurface() throws {
        let entry = try entry(for: .postCompose)
        let sut = NativeRouteDestinationView(entry: entry, turnstileSiteKey: turnstileSiteKey)

        XCTAssertNoThrow(try sut.inspect().find(button: "Publish"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save draft"))
        XCTAssertEqual(
            try sut.inspect()
                .find(text: "Title")
                .string(),
            "Title"
        )
    }

    func testDestinationViewRendersSettingsSurface() throws {
        let entry = try entry(for: .accountSettings)
        let sut = NativeRouteDestinationView(entry: entry)

        XCTAssertNoThrow(try sut.inspect().find(button: "Save Identity"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Create API Key"))
    }

    func testDestinationViewRendersListAndDetailSurfaces() throws {
        let list = try NativeRouteDestinationView(
            entry: entry(for: .communitiesBrowse),
            turnstileSiteKey: turnstileSiteKey
        )
        XCTAssertNoThrow(try list.inspect().find(ViewType.TextField.self))
        XCTAssertNoThrow(try list.inspect().find(button: "Search"))

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/builders"))
        let detail = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            turnstileSiteKey: turnstileSiteKey
        )
        XCTAssertNoThrow(try detail.inspect().find(button: "Join"))
        XCTAssertEqual(try detail.inspect().find(text: "Community").string(), "Community")
    }

    func testEveryIncludedDestinationIdentifierInitializesNativeDestinationView() {
        for entry in NativeRouteCatalog.includedEntries {
            let sut = NativeRouteDestinationView(entry: entry)

            XCTAssertEqual(sut.entry, entry)
        }
    }

    func testIncludedDestinationIdentifiersMatchTheCatalogAndExcludeAdminRoutes() {
        let includedDestinationIdentifiers = Set(NativeRouteCatalog.includedEntries.compactMap(\.destinationIdentifier))

        XCTAssertEqual(Set(NativeRouteDestinationIdentifier.allCases), includedDestinationIdentifiers)
        XCTAssertTrue(NativeRouteCatalog.excludedEntries.allSatisfy { $0.destinationIdentifier == nil })
    }

    func testExcludedRouteEntriesDoNotExposeNativeDestinationSurface() throws {
        let excluded = try XCTUnwrap(NativeRouteCatalog.excludedEntries.first)
        let auditMetadata = try XCTUnwrap(excluded.exclusionAuditMetadata)
        let sut = NativeRouteDestinationView(entry: excluded)
        let inspected = try sut.inspect()

        XCTAssertEqual(try inspected.find(text: "No native destination").string(), "No native destination")
        XCTAssertThrowsError(try inspected.find(text: auditMetadata.family))
        XCTAssertThrowsError(try inspected.find(text: auditMetadata.reason))
        XCTAssertNil(excluded.destinationIdentifier)
    }

    private func destinations(in section: AppSection) -> [NativeRouteDestinationIdentifier] {
        section.nativeParityGroups.flatMap(\.entries).compactMap(\.destinationIdentifier)
    }

    private func destinations(
        in section: AppSection,
        featureFlags: [String: Bool]
    ) -> [NativeRouteDestinationIdentifier] {
        section.nativeParityGroups(featureFlags: featureFlags)
            .flatMap(\.entries)
            .compactMap(\.destinationIdentifier)
    }

    private func filteredDestinations(
        in section: AppSection,
        isSignedIn: Bool,
        userRoles: [String]
    ) -> [NativeRouteDestinationIdentifier] {
        section.nativeParityGroups(isSignedIn: isSignedIn, userRoles: userRoles)
            .flatMap(\.entries)
            .compactMap(\.destinationIdentifier)
    }

    private func entry(for destination: NativeRouteDestinationIdentifier) throws -> NativeRouteCatalogEntry {
        try XCTUnwrap(NativeRouteCatalog.includedEntries.first { $0.destinationIdentifier == destination })
    }
}
