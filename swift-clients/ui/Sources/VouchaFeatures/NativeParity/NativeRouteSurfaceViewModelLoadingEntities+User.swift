import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadUserProfileRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        guard let match = routeMatch, let scope = NativeUserProfileScope(match: match) else {
            throw VouchaError.api(statusCode: 404, preconditionCode: nil)
        }
        let profile: UserProfileResponse = try await client.send(.user(
            idOrSlug: routeEntityId,
            includeBio: true
        ))
        userProfile.scope = scope
        userProfile.header = profile
        await configureProfileRelations(profile: profile, client: client)

        if scope == .overview {
            userProfile.pagination.reset()
            return await overviewRows(profile: profile, client: client)
        }
        let page = try await loadUserProfileCollection(
            scope: scope,
            userId: profile.user.id,
            after: nil,
            client: client
        )
        userProfile.collection = page.collection
        userProfile.pagination.replaceItems(page.collection.paginationItems)
        userProfile.pagination.restoreContinuation(
            endCursor: page.pageInfo.endCursor,
            hasMore: page.pageInfo.hasNextPage
        )
        return []
    }

    private func configureProfileRelations(profile: UserProfileResponse, client: APIClient) async {
        let user = profile.user
        detailRelationEntityType = "user"
        detailRelationEntityId = user.id
        detailReportTarget = .user(id: user.id)
        async let identityResponse: NativeIdentityResponse = client.send(.myIdentity)
        let bookmarks: EntityBookmarksResponse? = try? await client.send(.bookmarks(
            entityType: "user",
            entityId: user.id
        ))
        let identity = try? await identityResponse
        guard detailRelationEntityId == user.id else { return }
        detailRelationBookmarks = bookmarks?.bookmarks ?? [:]
        detailRelationIsSelfProfile = user.id == identity?.identity.id
        if detailRelationIsSelfProfile {
            detailReportTarget = nil
        } else if identity != nil {
            try? await loadUserTags(client: client)
            userProfile.trustContext = try? await client.send(.userTrustContext(userId: user.id))
            userProfile.trustChoice = userProfile.trustContext?.electionVote?.choice
        }
    }

    private func overviewRows(
        profile: UserProfileResponse,
        client: APIClient
    ) async -> [NativeRouteDestinationRow] {
        var result = profile.profileLinks.compactMap { link -> NativeRouteDestinationRow? in
            guard let url = link.url.flatMap(URL.init(string:)) else { return nil }
            return .init(
                icon: "link",
                title: (link.name ?? link.handle ?? url.host).map(rawText)
                    ?? appText(.nativeSwiftLandingPagesProfileLink),
                detail: rawText(url.absoluteString),
                externalURL: url
            )
        }
        guard isAdministrator,
              let landingPages: LandingPageListResponse = try? await client.send(
                  .adminUserLandingPages(userId: profile.user.id)
              )
        else { return result }
        result.append(row(
            "chart.line.uptrend.xyaxis",
            appText(.nativeSwiftNavigationTitlesAnalytics),
            countText(landingPages.results.count, item: "page")
        ))
        await result.append(contentsOf: adminLandingPageAnalyticsRows(client: client, pages: landingPages.results))
        return result
    }

    func adminLandingPageAnalyticsRows(
        client: APIClient,
        pages: [LandingPageSummary]
    ) async -> [NativeRouteDestinationRow] {
        var rows: [NativeRouteDestinationRow] = []
        for start in stride(from: 0, to: pages.count, by: 4) {
            let batch = Array(pages[start ..< min(start + 4, pages.count)])
            let ordered = await withTaskGroup(of: (Int, NativeRouteDestinationRow).self) { group in
                for (index, page) in batch.enumerated() {
                    group.addTask {
                        guard let analytics: AdminLandingPageAnalyticsResponse = try? await client.send(
                            .adminLandingPageAnalytics(pageId: page.id)
                        ) else {
                            return (index, NativeRouteDestinationRow(
                                icon: "chart.bar.xaxis",
                                title: .userContent(page.title),
                                detail: .joined([page.slug, page.subtitle]
                                    .compactMap { $0 }
                                    .map(UiVerbatimText.userContent))
                            ))
                        }
                        return (index, NativeRouteDestinationRow(
                            icon: "chart.bar.xaxis",
                            title: .userContent(analytics.landingPage.title),
                            detail: .joined([
                                .count(analytics.analytics.totalVisits, item: "visit"),
                                .count(analytics.analytics.totalClicks, item: "click"),
                                .count(analytics.analytics.conversionFunnel.totalSignups, item: "signup")
                            ])
                        ))
                    }
                }
                var batchRows: [(Int, NativeRouteDestinationRow)] = []
                for await item in group {
                    batchRows.append(item)
                }
                return batchRows.sorted { $0.0 < $1.0 }.map(\.1)
            }
            rows.append(contentsOf: ordered)
        }
        return rows
    }
}
