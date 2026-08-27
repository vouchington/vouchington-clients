import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadLibraryRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        switch destination {
        case .referrals:
            try await loadReferralRows(client: client)
        case .plans:
            try await loadPlanRows(client: client)
        case .bookmarks:
            try await loadBookmarkRows(client: client)
        case .landingPages:
            try await loadLandingPageRows(client: client)
        case .lists:
            try await loadListsPreviewPage(client: client, after: nil).rows.map(\.row)
        default:
            []
        }
    }

    func loadListsPreviewPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let response: ListsSearchResponse = try await client.send(.lists(after: after, limit: 25))
        let rows = response.results.compactMap { result -> NativeForwardRow? in
            guard let list = response.lists[result.id] else { return nil }
            return forwardRow(
                id: result.id,
                icon: "list.bullet.rectangle",
                title: rawText(list.name),
                detail: list.description.map(rawText) ?? listVisibilityText(list.visibility)
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    func loadAccountRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        switch destination {
        case .accountSettings:
            try await loadIdentityRows(client: client)
        case .profileSettings:
            try await loadProfileRows(client: client)
        case .advancedSettings, .notificationSettings:
            try await loadAdvancedSettingsRows(client: client)
        default:
            []
        }
    }

    private func loadIdentityRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: NativeIdentityResponse = try await client.send(.myIdentity)
        return [
            row(
                "person.crop.circle",
                rawText(response.identity.username),
                appText(.nativeSwiftRouteSurfaceIdentity)
            ),
            row(
                "checkmark.seal",
                response.identity.verificationStatus.map(rawText)
                    ?? appText(.nativeSwiftRouteSurfaceUnverified),
                appText(.nativeSwiftRouteSurfaceVerificationStatus)
            ),
            row(
                "creditcard",
                response.identity.membershipPlan.map(rawText)
                    ?? appText(.nativeSwiftMembershipFree),
                appText(.nativeSwiftMembershipMembership)
            )
        ]
    }

    private func loadProfileRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        async let profileResponse: NativeProfileResponse = client.send(.myProfile)
        async let linksResponse: NativeListResponse = client.send(Endpoint(.GET, path: "/api/v1/my/profile/links"))
        let profile = try await profileResponse
        let links = try await linksResponse

        return [
            row(
                "person.text.rectangle",
                appText(
                    profile.profile.markdown.isEmpty
                        ? .nativeSwiftRouteSurfaceNoBio
                        : .nativeSwiftProfileBio
                ),
                rawText(profile.profile.markdown)
            ),
            row(
                "link",
                appText(.nativeSwiftSettingsProfileLinks),
                countText(links.results.count, item: "link")
            )
        ]
    }

    private func loadAdvancedSettingsRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        async let apiKeysResponse: NativeApiKeysResponse = client.send(Endpoint(.GET, path: "/api/v1/my/api-keys"))
        async let asidePreferencesResponse: NativeAsidePreferencesResponse = client.send(
            Endpoint(.GET, path: "/api/v1/my/aside-preferences")
        )
        async let consentsResponse: NativeConsentsResponse = client.send(Endpoint(.GET, path: "/api/v1/my/consents"))
        async let contributionStatusResponse: NativeContributionStatusResponse = client.send(
            Endpoint(.GET, path: "/api/v1/my/contribution-status")
        )

        let apiKeys = try await apiKeysResponse
        let asidePreferences = try await asidePreferencesResponse
        let consents = try await consentsResponse
        let contributionStatus = try await contributionStatusResponse

        return [
            row(
                "key",
                appText(.nativeSwiftSettingsApiKeys),
                nativeListItemDetail(apiKeys.results, item: "key")
            ),
            row(
                "slider.horizontal.3",
                appText(.nativeSwiftRouteSurfaceDismissedAsides),
                asidePreferenceDetail(asidePreferences.asidePreferences)
            ),
            row(
                "hand.raised",
                appText(.nativeSwiftRouteSurfaceConsents),
                nativeListItemDetail(consents.results, item: "item")
            ),
            row(
                "chart.bar",
                appText(.nativeSwiftRouteSurfaceContribution),
                contributionStatus.contributionStatus.allowed
                    ? appText(
                        .nativeSwiftRouteSurfaceAllowedQuota,
                        numberParameters: [
                            "used": Double(contributionStatus.dailyQuota.used),
                            "limit": Double(contributionStatus.dailyQuota.limit)
                        ]
                    )
                    : contributionStatus.contributionStatus.reason.map(rawText)
                    ?? appText(.nativeSwiftRouteSurfaceRestricted)
            )
        ]
    }

}
