import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadModerationRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        guard let path = routeMatch?.path else {
            return try await loadAppealRows(client: client, mine: true)
        }

        if let rows = try await loadModerationCasesRows(client: client, path: path) {
            return rows
        }

        if let rows = try await loadUserModerationRows(client: client, path: path) {
            return rows
        }
        if let rows = try await loadStaffModerationRows(client: client, path: path) {
            return rows
        }
        return try await loadAppealRows(client: client, mine: true)
    }

    private func loadModerationCasesRows(
        client: APIClient,
        path: String
    ) async throws -> [NativeRouteDestinationRow]? {
        if destination == .moderationTransparency {
            return try await loadModerationTransparencyRows(client: client)
        }
        guard destination == .moderationCases else { return nil }
        switch path {
        case "/reports":
            return try await loadModerationCountRows(
                client: client,
                path: "/api/v1/reports",
                title: .nativeSwiftRouteSurfaceReports,
                icon: "flag"
            )
        default:
            return nil
        }
    }

    private func loadModerationTransparencyRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let loadRevision = moderationTransparencyLoadRevision
        moderationTransparencyPageRevision += 1
        let pageRequestRevision = moderationTransparencyPageRevision
        let range = moderationTransparencyRange
        do {
            let response: ModerationTransparency = try await client.send(
                .moderationTransparency(range: range.rawValue)
            )
            guard ownsModerationTransparencyRequest(
                loadRevision: loadRevision,
                pageRequestRevision: pageRequestRevision,
                range: range
            ) else { return rows }
            moderationTransparencyBuckets = response.buckets
            moderationTransparencyNextCursor = response.nextCursor
            moderationTransparencyLoadMoreError = nil
            return moderationTransparencyRows(moderationTransparencyBuckets, range: range)
        } catch let error as VouchaError {
            guard ownsModerationTransparencyRequest(
                loadRevision: loadRevision,
                pageRequestRevision: pageRequestRevision,
                range: range
            ) else { return rows }
            switch error {
            case .forbidden, .notFound: break
            default: throw error
            }
            moderationTransparencyBuckets = []
            moderationTransparencyNextCursor = nil
            moderationTransparencyLoadMoreError = nil
            return moderationTransparencyLockedRows()
        }
    }

    func moderationTransparencyLockedRows() -> [NativeRouteDestinationRow] {
        [row(
            "lock",
            appText(.nativeSwiftCommunityRowsModerationTransparency),
            appText(.nativeSwiftCommunityRowsTransparencyLocked)
        )]
    }

    var moderationTransparencyRange: ModerationTransparencyRange {
        guard destination == .moderationTransparency,
              let rawValue = nativeRouteQueryItems(fromEncodedQuery: routeQuery)["range"],
              let range = ModerationTransparencyRange(rawValue: rawValue),
              range != .unknown
        else {
            return .days30
        }
        return range
    }

    private func loadUserModerationRows(
        client: APIClient,
        path: String
    ) async throws -> [NativeRouteDestinationRow]? {
        switch path {
        case _ where path.contains("warnings"):
            try await loadModerationCountRows(
                client: client,
                path: "/api/v1/my/warnings",
                title: .nativeSwiftRouteSurfaceWarnings,
                icon: "exclamationmark.triangle"
            )
        case _ where path.contains("bans"):
            try await loadModerationCountRows(
                client: client,
                path: "/api/v1/my/bans",
                title: .nativeSwiftCommunitiesBans,
                icon: "hand.raised"
            )
        case _ where path.contains("removed-posts"):
            try await loadModerationCountRows(
                client: client,
                path: "/api/v1/my/removed-posts",
                title: .nativeSwiftRouteSurfaceRemovedPosts,
                icon: "trash"
            )
        case "/appeals":
            try await loadAppealRows(client: client, mine: false)
        case _ where path.hasPrefix("/my/") && path.contains("disputes"):
            try await loadMyDisputeRows(client: client)
        default:
            nil
        }
    }

    private func loadAppealRows(client: APIClient, mine: Bool) async throws -> [NativeRouteDestinationRow] {
        let response: ModerationAppealListResponse = try await client.send(.appeals(limit: 25, mine: mine))
        return response.appeals.map {
            row("arrow.uturn.left.circle", rawText($0.caseId ?? $0.id), appText($0.status.titleKey))
        }
    }

    private func loadMyDisputeRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: ModerationDisputeListResponse = try await client.send(.disputes(limit: 25, mine: true))
        return response.disputes.map {
            row("exclamationmark.triangle", rawText($0.postId ?? $0.id), appText($0.status.titleKey))
        }
    }

    private func loadModerationCountRows(
        client: APIClient,
        path: String,
        title: UiMessageKey,
        icon: String
    ) async throws -> [NativeRouteDestinationRow] {
        let response: NativeListResponse = try await client.send(Endpoint(.GET, path: path, queryItems: [
            URLQueryItem(name: "limit", value: "25")
        ]))
        return [row(icon, appText(title), countText(response.results.count, item: "item"))]
    }
}
