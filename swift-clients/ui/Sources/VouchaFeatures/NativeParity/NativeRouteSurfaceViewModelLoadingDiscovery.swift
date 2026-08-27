import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadDiscoveryRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        switch destination {
        case .fediverseInstances:
            return try await loadFediverseInstanceRows(client: client)
        case .communitiesBrowse:
            let response: NativeGenericListResponse = try await client.send(Endpoint(
                .GET,
                path: "/api/v1/communities",
                queryItems: [URLQueryItem(name: "limit", value: "25")]
            ))
            return response.results.map {
                let community = response.hydratedEntity(for: $0)
                return row(
                    "person.3",
                    rawText(community.displayTitle(fallback: "")),
                    rawText(community.displayDetail)
                )
            }
        case .feedReferralLinks:
            return try await loadReferralFeedPage(client: client, after: nil).rows.map(\.row)
        case .topicRecommendations:
            return try await loadTopicRecommendationRows(client: client)
        default:
            return []
        }
    }

    func loadReferralFeedPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let feedType = routeMatch?.path == "/feed/referral-links/mutual" ? "mutual_follows" : "follow_users"
        let response: ReferralLinkFeedResponse = try await client.send(
            .referralLinksFeed(feedType: feedType, after: after, limit: 10)
        )
        let rows = response.results.map {
            forwardRow(
                id: $0.id,
                icon: "link",
                title: rawText($0.label ?? $0.url ?? $0.id),
                detail: $0.userId.map(rawText) ?? appText(.nativeSwiftRouteSurfaceReferralLink)
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    func postTypeIcon(for postType: PostType) -> String {
        switch postType {
        case .discussion, .review: "doc.text"
        case .dataPoint: "chart.bar"
        case .comment: "bubble.left"
        case .article, .blogPost: "newspaper"
        case .story: "photo.on.rectangle"
        case .link: "link"
        case .topicRecommendation: "lightbulb"
        }
    }

    func itemIcon(for mediaType: String?) -> String {
        switch mediaType {
        case "audio": "headphones"
        case "video": "play.rectangle"
        default: "newspaper"
        }
    }
}

extension NativeRouteSurfaceViewModel {
    func loadFediverseInstanceRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: FediverseInstancesResponse = try await client.send(.fediverseInstances(
            query: fediverseInstanceQuery,
            sort: fediverseInstanceSort
        ))
        fediverseInstanceItems = Self.uniqueFediverseInstances(response.orderedInstances, excluding: [])
        fediverseInstancePageInfo = response.pageInfo
        fediverseInstancePaginationErrorMessage = nil
        return fediverseRows(from: fediverseInstanceItems)
    }

    public func loadMoreFediverseInstances() async {
        guard let client, destination == .fediverseInstances,
              fediverseInstancePageInfo?.hasNextPage == true,
              let cursor = fediverseInstancePageInfo?.endCursor,
              !isLoadingMoreFediverseInstances else { return }
        isLoadingMoreFediverseInstances = true
        defer { isLoadingMoreFediverseInstances = false }
        do {
            let response: FediverseInstancesResponse = try await client.send(
                .fediverseInstances(
                    after: cursor,
                    query: fediverseInstanceQuery,
                    sort: fediverseInstanceSort
                )
            )
            fediverseInstanceItems += Self.uniqueFediverseInstances(
                response.orderedInstances,
                excluding: Set(fediverseInstanceItems.map(\.id))
            )
            fediverseInstancePageInfo = response.pageInfo
            fediverseInstancePaginationErrorMessage = nil
            rows = fediverseRows(from: fediverseInstanceItems)
        } catch {
            fediverseInstancePaginationErrorMessage = .message(.nativeSwiftRouteSurfaceLoadMoreFailed)
        }
    }

    private func fediverseRows(from items: [FediverseInstanceListItem]) -> [NativeRouteDestinationRow] {
        items.map { item in
            let software = [item.instance.software, item.instance.nodeinfoSoftwareVersion]
                .compactMap { $0 }.joined(separator: " ")
            return NativeRouteDestinationRow(
                icon: "server.rack",
                title: .userContent(item.topic.name),
                detail: .message(
                    .nativeSwiftRouteSurfaceInstanceSummary,
                    textParameters: [
                        "software": software
                            .isEmpty ? .message(.nativeSwiftRouteSurfaceUnclassified) : .externalProvider(software),
                        "trust": FediverseTrustTier(election: item.hostnameElection).label
                    ]
                ),
                targetPath: "/instance/\(item.topic.slug)"
            )
        }
    }

    static func uniqueFediverseInstances(
        _ items: [FediverseInstanceListItem],
        excluding existing: Set<String>
    ) -> [FediverseInstanceListItem] {
        var seen = existing
        return items.filter { seen.insert($0.id).inserted }
    }

    private var fediverseInstanceQuery: String? {
        guard let query = routeMatch?.queryValue("q")?.trimmingCharacters(in: .whitespacesAndNewlines),
              !query.isEmpty else { return nil }
        return query
    }

    private var fediverseInstanceSort: String {
        guard let sort = routeMatch?.queryValue("sort"),
              ["best", "new", "relevance"].contains(sort) else { return "best" }
        return sort
    }
}
