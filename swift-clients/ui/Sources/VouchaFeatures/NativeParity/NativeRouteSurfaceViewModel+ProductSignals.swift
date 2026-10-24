import Foundation
import VouchaAPI

extension NativeRouteSurfaceViewModel {
    func loadPublicLandingRows(
        client: APIClient,
        endpoint: Endpoint
    ) async throws -> [NativeRouteDestinationRow] {
        let response: NativePublicLandingPageResponse = try await client.send(endpoint)
        let page = response.landingPage
        recordProductSignalOnce(
            "landing-visit:\(page.id)",
            endpoint: .landingPageVisit(landingPageId: page.id, signal: landingPageVisitSignal()),
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
        if isAdministrator {
            if let analytics: AdminLandingPageAnalyticsResponse = try? await client.send(
                .adminLandingPageAnalytics(pageId: page.id)
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
        guard isSignedIn else { return }
        productSignalTask = Task {
            do {
                let _: EmptyResponse = try await client.send(endpoint)
            } catch {
                return
            }
        }
    }

    func landingPageVisitSignal() -> LandingPageVisitSignal {
        let items = nativeRouteQueryItems(fromEncodedQuery: routeQuery)
        return LandingPageVisitSignal(
            utmSource: productSignalText(items["utm_source"]) ?? productSignalText(items["ref"]),
            utmMedium: productSignalText(items["utm_medium"]),
            utmCampaign: productSignalText(items["utm_campaign"]),
            utmContent: productSignalText(items["utm_content"])
        )
    }
}

private func productSignalText(_ value: String?) -> String? {
    guard let value else { return nil }
    let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
    return trimmed.isEmpty ? nil : trimmed
}
