import Foundation
import VouchaAPI

extension NativeRouteSurfaceViewModel {
    func loadPublicLandingRows(
        client: APIClient,
        endpoint: Endpoint
    ) async throws -> [NativeRouteDestinationRow] {
        let response: NativePublicLandingPageResponse = try await client.send(endpoint)
        guard productSignalsActive else { return [] }
        let page = response.landingPage
        publicLandingPage = page
        recordProductSignalOnce(
            "landing-visit:\(page.id)",
            endpoint: .landingPageVisit(landingPageId: page.id, signal: LandingPageVisitSignal()),
            client: client
        )
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
        if isAdministrator, let username = response.user.username {
            if let analytics: AdminLandingPageAnalyticsResponse = try? await client.send(
                .adminLandingPageAnalytics(username: username, slug: page.slug)
            ) {
                rows.append(contentsOf: landingPageAnalyticsRows(analytics.analytics))
            }
        }
        return rows
    }

    func recordRssFeedView(feedId: String, client: APIClient) {
        recordProductSignalOnce(
            "rss-feed-view:\(feedId)",
            endpoint: .rssFeedView(rssFeedId: feedId),
            client: client
        )
    }

    func recordProductSignalOnce(_ key: String, endpoint: Endpoint, client: APIClient) {
        guard recordedProductSignalKeys.insert(key).inserted else { return }
        dispatchProductSignal(endpoint, client: client)
    }

    func recordPublicLandingItemClick(
        landingPageId: String,
        itemId: String,
        groupMemberId: String? = nil
    ) {
        guard let client else { return }
        dispatchProductSignal(
            .landingPageClick(
                landingPageId: landingPageId,
                signal: LandingPageClickSignal(
                    landingPageItemId: itemId,
                    groupMemberId: groupMemberId
                )
            ),
            client: client
        )
    }

    private func dispatchProductSignal(_ endpoint: Endpoint, client: APIClient) {
        guard isSignedIn, productSignalsActive, productSignalTasks.count < 4 else { return }
        let id = UUID()
        let task = Task {
            defer { productSignalTasks.removeValue(forKey: id) }
            guard productSignalsActive, !Task.isCancelled else { return }
            do {
                try await withThrowingTaskGroup(of: Void.self) { group in
                    group.addTask {
                        let _: EmptyResponse = try await client.send(endpoint)
                    }
                    group.addTask {
                        try await Task.sleep(for: .seconds(5))
                    }
                    defer { group.cancelAll() }
                    _ = try await group.next()
                }
            } catch {
                return
            }
        }
        productSignalTasks[id] = task
        productSignalTask = task
    }

    func beginProductSurfaceActivation() {
        guard !productSurfaceVisible, productSignalsActive else { return }
        productSurfaceVisible = true
        recordedProductSignalKeys.removeAll()
        if let page = publicLandingPage, let client {
            recordProductSignalOnce(
                "landing-visit:\(page.id)",
                endpoint: .landingPageVisit(landingPageId: page.id, signal: LandingPageVisitSignal()),
                client: client
            )
        }
    }

    func endProductSurfaceActivation() {
        productSurfaceVisible = false
    }

    func invalidateProductSignals() {
        productSignalsActive = false
        for task in productSignalTasks.values {
            task.cancel()
        }
        productSignalTasks.removeAll()
        publicLandingPage = nil
    }

}
