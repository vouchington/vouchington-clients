import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadManagementRows(
        client: APIClient,
        after: String?,
        tab: CommunitySurfaceTab,
        revision: Int
    ) async throws -> CommunityForwardPage {
        switch tab {
        case .settings:
            .terminal(loadSettingsRows())
        case .moderation:
            try await .terminal(loadModerationRows(client: client, tab: tab, revision: revision))
        case .modlog:
            try await loadModlogRows(client: client, after: after)
        case .modmail:
            try await .terminal(loadModmailRows(client: client))
        case .moderatorVacation:
            try await .terminal(loadModeratorVacationRows(client: client))
        case .bans:
            try await loadBansRows(client: client, after: after)
        case .restrictions:
            try await loadRestrictionsRows(client: client, after: after)
        case .moderationAnalytics:
            try await .terminal(
                canViewRawModerationAnalytics
                    ? loadModerationAnalyticsRows(client: client, revision: revision, tab: tab)
                    : loadCommunityModerationTransparencyRows(client: client)
            )
        default:
            .terminal([])
        }
    }

    func hydrateVacationPreferenceIfControlsVisible(
        client: APIClient,
        tab: CommunitySurfaceTab,
        revision: Int
    ) async throws {
        guard tab != .moderatorVacation,
              tab != .modmail,
              canManageModeratorVacation,
              tab.isManagementTab || tab == .moderation
        else { return }
        let response = try await fetchModeratorVacation(client: client)
        guard isCurrentCommunityLoad(revision, tab: tab) else { return }
        suppressCommunityDigestsWhileOnVacation = response.suppressCommunityDigestsWhileOnVacation
    }

    func loadModeratorVacationRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        guard canManageModeratorVacation else { return [] }
        let response = try await loadModeratorVacation(client: client)
        guard let vacation = response.vacation else { return [] }
        return [
            .init(
                icon: "moon.zzz",
                title: UiMessage(.nativeSwiftCommunitiesModeratorVacation),
                detail: vacation.endsAt.map {
                    UiMessage(
                        .nativeSwiftCommunitiesActiveUntil,
                        dateParameters: ["date": .init($0, dateStyle: .abbreviated)]
                    )
                } ?? UiMessage(.nativeSwiftCommunityRowsActive)
            )
        ]
    }

    private func loadModeratorVacation(client: APIClient) async throws -> ModeratorVacationResponse {
        let response = try await fetchModeratorVacation(client: client)
        suppressCommunityDigestsWhileOnVacation = response.suppressCommunityDigestsWhileOnVacation
        return response
    }

    private func fetchModeratorVacation(client: APIClient) async throws -> ModeratorVacationResponse {
        try await client.send(
            .communityModeratorVacation(idOrSlug: slug)
        )
    }
}
