import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadEntityRows(for destination: NativeRouteDestinationIdentifier, client: APIClient) async throws
        -> [NativeRouteDestinationRow] {
        if let list = destination.entityListSurface {
            return try await loadGenericListRows(
                client: client,
                path: list.path,
                icon: list.icon,
                title: list.title
            )
        }

        return switch destination {
        case .topicDetail:
            try await loadTopicDetailRows(client: client)
        case .domainDetail:
            try await loadHostnameDetailRows(client: client)
        case .urlDetail:
            try await loadUrlDetailRows(client: client)
        case .userProfile:
            try await loadUserProfileRows(client: client)
        case .communityDetail:
            try await loadCommunityDetailRows(client: client)
        case .compare:
            try await loadCompareRows(client: client)
        default:
            []
        }
    }

    var routeEntityId: String {
        routeMatch?.param(
            "id",
            "idOrSlug",
            "idOrHostname",
            "idOrUsername",
            "slug",
            "username"
        ) ?? routeMatch?.path.routeLastSegment ?? ""
    }

    private func loadGenericListRows(
        client: APIClient,
        path: String,
        icon: String,
        title: UiMessageKey
    ) async throws -> [NativeRouteDestinationRow] {
        let response: NativeGenericListResponse = try await client.send(
            Endpoint(.GET, path: path, queryItems: [.init(name: "limit", value: "25")])
        )
        if response.results.isEmpty {
            return [row(icon, appText(title), appText(.nativeSwiftRouteSurfaceNoResults))]
        }
        return response.results.enumerated().map { index, result in
            let entity = response.hydratedEntity(for: result)
            let authoredTitle = entity.normalizedAuthoredTitle
            return row(
                icon,
                authoredTitle?.text
                    ?? .verbatim(entity.displayTitle(fallback: entity.id.ifNotEmpty ?? String(index + 1))),
                topicBrowseDetail(for: entity, fallback: entity.displayDetail, response: response),
                declaredLanguage: authoredTitle?.declaredLanguage,
                detectedLanguage: authoredTitle?.detectedLanguage,
                provenance: entity.provenance
            )
        }
    }

    private func topicBrowseDetail(
        for entity: NativeGenericEntity,
        fallback: String,
        response: NativeGenericListResponse
    ) -> UiVerbatimText {
        guard response.topics?[entity.id] != nil,
              let election = response.topicElections?[entity.id] else {
            return .verbatim(fallback)
        }
        let myVote = response.electionVotes?[entity.id]?.choice ?? election.myVote
        if let myVote {
            myVotesByTopicId[entity.id] = myVote
        }
        return .joined([
            .count(election.votesCountUp, item: "positiveVote"),
            .count(election.votesCountDown, item: "negativeVote")
        ])
    }

    private func loadTopicDetailRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        if routeMatch?.path.hasPrefix("/instance/") == true {
            return try await loadFediverseInstanceDetailRows(client: client)
        }
        let response: NativeTopicDetailResponse = try await client.send(.topic(id: routeEntityId))
        applyTopicDetailResponse(response)
        let bookmarks: EntityBookmarksResponse? = await (try? client.send(.bookmarks(
            entityType: "topic",
            entityId: response.topic.id
        )))
        let relationBookmarks = bookmarks?.bookmarks ?? [:]
        detailRelationEntityType = "topic"
        detailRelationEntityId = response.topic.id
        detailRelationBookmarks = relationBookmarks
        detailRelationIsSelfProfile = false
        let entity = response.topic
        let voteSummary = topicVoteSummary(for: entity.id)
        let voteDetail = voteSummary.map {
            .joined([
                .count($0.votesCountUp, item: "positiveVote"),
                .count($0.votesCountDown, item: "negativeVote")
            ])
        } ?? appText(.nativeSwiftRouteSurfaceVoteThisTopic)
        return [
            row(
                "tag",
                .verbatim(entity.displayTitle(fallback: routeEntityId)),
                .verbatim(entity.displayDetail),
                provenance: entity.provenance
            ),
            row("chevron.up.chevron.down", appText(.nativeSwiftRouteSurfaceVote), voteDetail),
            row("doc.text", countText(0, item: "post"), appText(.nativeSwiftRouteSurfaceTopicPostsAvailable)),
            row(
                "newspaper",
                appText(.nativeSwiftNavigationTitlesNews),
                appText(.nativeSwiftRouteSurfaceTopicNewsAvailable)
            )
        ]
    }

}
