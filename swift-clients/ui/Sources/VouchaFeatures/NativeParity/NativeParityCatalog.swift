import VouchaLocalization

enum NativeParityCatalog {
    static func groupsBySection(featureFlags: [String: Bool]) -> [AppSection: [NativeRouteFamilyGroup]] {
        [
            .discover: discoverGroups(featureFlags: featureFlags),
            .topics: topicGroups,
            .communities: communityGroups,
            .messages: messageGroups,
            .settings: settingsGroups,
            .library: libraryGroups,
            .actions: actionGroups,
            .moderation: moderationGroups,
            .crm: crmGroups,
            .engineering: engineeringGroups,
            .growth: growthGroups
        ]
    }

    static func discoverGroups(featureFlags: [String: Bool] = [:]) -> [NativeRouteFamilyGroup] {
        [
            .init(
                title: UiMessage(.nativeSwiftRouteFamilyDirectoryDiscoveryTitle),
                summary: UiMessage(.nativeSwiftRouteFamilyDirectoryDiscoverySummary),
                icon: "magnifyingglass",
                entries: discoveryEntries(featureFlags: featureFlags)
            ),
            .init(
                title: UiMessage(.nativeSwiftRouteFamilyDirectoryPeopleTitle),
                summary: UiMessage(.nativeSwiftRouteFamilyDirectoryPeopleSummary),
                icon: "person.2",
                entries: entries(for: .usersBrowse, .userProfile)
            )
        ]
    }

    private static func discoveryEntries(featureFlags: [String: Bool]) -> [NativeRouteCatalogEntry] {
        var destinations: [NativeRouteDestinationIdentifier] = [.webSearch]
        if featureFlags["fediverse"] == true {
            destinations.append(.fediverseSearch)
            destinations.append(.fediverseInstances)
        }
        destinations += [.feedReferralLinks, .postsBrowse, .storiesBrowse]
        return destinations.map(entry(for:))
    }

    static func entries(for destinations: NativeRouteDestinationIdentifier...) -> [NativeRouteCatalogEntry] {
        destinations.map(entry(for:))
    }

    static func entry(for destination: NativeRouteDestinationIdentifier) -> NativeRouteCatalogEntry {
        guard let entry = NativeRouteCatalog.entries.first(where: { $0.destinationIdentifier == destination }) else {
            preconditionFailure("Missing native route catalog entry for \(destination.rawValue)")
        }
        return entry
    }
}
