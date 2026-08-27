import Foundation
import VouchaAPI

extension NativeRouteSurfaceViewModel {
    func loadBookmarkRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let identity: NativeIdentityResponse = try await client.send(.myIdentity)
        let userId = identity.identity.id
        if let collection = bookmarkCollection(for: userId) {
            currentBookmarkCollection = collection
            try await loadInitialBookmarkPage(collection: collection, client: client)
            return []
        }

        async let postsResponse: NativeListResponse = client.send(Endpoint(
            .GET,
            path: "/api/v1/users/\(userId)/posts/saved"
        ))
        async let feedItemsResponse: NativeListResponse = client.send(
            Endpoint(.GET, path: "/api/v1/users/\(userId)/rss-feed-items/saved")
        )
        async let urlsResponse: NativeListResponse = client.send(Endpoint(
            .GET,
            path: "/api/v1/users/\(userId)/urls/saved"
        ))
        async let communitiesResponse: NativeListResponse = client.send(
            Endpoint(.GET, path: "/api/v1/users/\(userId)/communities/saved")
        )

        let posts = try await postsResponse
        let feedItems = try await feedItemsResponse
        let urls = try await urlsResponse
        let communities = try await communitiesResponse

        return [
            row("doc.text", appText(.nativeSwiftRouteSurfaceSavedPosts), countText(posts.results.count, item: "item")),
            row(
                "headphones",
                appText(.nativeSwiftRouteSurfaceSavedFeedItems),
                countText(feedItems.results.count, item: "item")
            ),
            row("link", appText(.nativeSwiftRouteSurfaceSavedUrls), countText(urls.results.count, item: "item")),
            row(
                "person.3",
                appText(.nativeSwiftRouteSurfaceSavedCommunities),
                countText(communities.results.count, item: "item")
            )
        ]
    }

    func loadLandingPageRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        if let publicLandingEndpoint {
            let response: NativePublicLandingPageResponse = try await client.send(publicLandingEndpoint)
            let page = response.landingPage
            var rows = [
                row(
                    "doc.text",
                    rawText(page.title),
                    .joined([
                        rawText(page.slug),
                        page.isDefault ? appText(.nativeSwiftCommunityRowsDefaultValue) : nil,
                        page.subtitle.map(rawText)
                    ].compactMap { $0 })
                )
            ]
            if isAdministrator {
                if let analytics: AdminLandingPageAnalyticsResponse = try? await client.send(
                    .adminLandingPageAnalytics(pageId: page.id)
                ) {
                    rows.append(contentsOf: landingPageAnalyticsRows(analytics.analytics))
                }
            }
            return rows
        }
        if let slug = routeMatch?.param("slug") {
            return try await loadMyLandingPageRows(client: client, slug: slug)
        }

        let pages: NativeLandingPagesResponse = try await client.send(.myLandingPages)
        let candidates: NativeLandingPageCandidatesResponse = try await client.send(.myLandingPageCandidates)

        var rows = pages.results.map {
            row(
                "doc.text",
                rawText($0.title),
                .joined([
                    rawText($0.slug),
                    $0.isDefault ? appText(.nativeSwiftCommunityRowsDefaultValue) : nil,
                    $0.subtitle.map(rawText)
                ].compactMap { $0 })
            )
        }

        let candidateDetail = [
            nativeListItemDetail(candidates.candidates.profileLinks, item: "link"),
            nativeListItemDetail(candidates.candidates.reviews, item: "review"),
            nativeListItemDetail(candidates.candidates.referralLinks, item: "link")
        ]
        rows.append(row(
            "sparkles",
            appText(.nativeSwiftRouteSurfaceLandingPageCandidates),
            .joined(candidateDetail)
        ))

        return rows
    }

    private func loadMyLandingPageRows(client: APIClient, slug: String) async throws -> [NativeRouteDestinationRow] {
        let pages: NativeLandingPagesResponse = try await client.send(.myLandingPages)
        guard let page = pages.results.first(where: { $0.slug == slug }) else {
            return [row(
                "doc.text",
                appText(.nativeSwiftRouteSurfaceLandingPage),
                appText(.nativeSwiftRouteSurfaceNoPageFound, parameters: ["slug": slug])
            )]
        }

        if routeMatch?.template == "/my/landing-page/:slug/analytics" {
            let response: LandingPageAnalyticsResponse = try await client.send(.myLandingPageAnalytics(pageId: page.id))
            return landingPageAnalyticsRows(response.analytics, title: page.title)
        }

        let response: NativeMyLandingPageResponse = try await client.send(.myLandingPage(id: page.id))
        return [
            row(
                "doc.text",
                rawText(response.landingPage.title),
                rawText(response.landingPage.slug)
            ),
            row(
                "list.bullet",
                appText(.nativeSwiftRouteSurfaceItems),
                nativeListItemDetail(response.landingPage.items, item: "item")
            )
        ]
    }

    private var publicLandingEndpoint: Endpoint? {
        guard let routeMatch, routeMatch.path.hasPrefix("/my/") == false else { return nil }
        guard let username = routeMatch.param("username") ?? routeMatch.param("idOrUsername") ?? routeMatch.param("id")
        else {
            return nil
        }
        if let slug = routeMatch.param("slug") {
            return .publicLandingPage(username: username, slug: slug)
        }
        return .publicDefaultLandingPage(username: username)
    }

    private func landingPageAnalyticsRows(
        _ analytics: LandingPageAnalytics,
        title: String? = nil
    ) -> [NativeRouteDestinationRow] {
        [
            row(
                "chart.line.uptrend.xyaxis",
                title.map(rawText) ?? appText(.nativeSwiftRouteSurfaceVisits),
                countText(analytics.totalVisits, item: "visit")
            ),
            row(
                "cursorarrow.click",
                appText(.nativeSwiftRouteSurfaceClicks),
                countText(analytics.totalClicks, item: "click")
            ),
            row(
                "person.2",
                appText(.nativeSwiftRouteSurfaceUniqueVisitors),
                countText(analytics.uniqueVisitors, item: "visitor")
            ),
            row(
                "person.badge.plus",
                appText(.nativeSwiftRouteSurfaceSignups),
                countText(analytics.conversionFunnel.totalSignups, item: "signup")
            )
        ]
    }

}
