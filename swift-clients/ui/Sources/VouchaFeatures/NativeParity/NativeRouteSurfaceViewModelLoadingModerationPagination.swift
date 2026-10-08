import VouchaAPI
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadModerationForwardPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let path = routeMatch?.path
        if path?.contains("vote-integrity") == true {
            return try await loadVoteIntegrityPage(client: client, after: after)
        }
        if path?.contains("report-integrity") == true {
            return try await loadReportIntegrityPage(client: client, after: after)
        }
        if path == "/admin/modlog" {
            return try await loadAdminModlogPage(client: client, after: after)
        }
        if path?.contains("disputes") == true {
            return path?.hasPrefix("/my/") == true
                ? try await loadMyDisputePage(client: client, after: after)
                : try await loadReviewDisputePage(client: client, after: after)
        }
        return try await loadAppealPage(client: client, after: after, mine: path != "/appeals")
    }

    private func loadVoteIntegrityPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let response: VoteIntegrityFlagsResponse = try await client.send(.voteIntegrityFlags(
            status: .pending,
            after: after,
            limit: 25
        ))
        let rows = response.results.map {
            forwardRow(
                id: $0.id,
                icon: "shield.lefthalf.filled",
                title: rawText($0.flagType),
                detail: .joined([
                    appText($0.resolvedAt == nil ? .nativeSwiftRouteSurfacePending : .nativeSwiftRouteSurfaceResolved),
                    rawText($0.postId ?? $0.topicId ?? $0.hostnameId ?? $0.entityRelationId
                        ?? $0.agentModerationId ?? $0.id)
                ])
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    private func loadReportIntegrityPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let response: ReportIntegrityFlagsResponse = try await client.send(.reportIntegrityFlags(
            status: .pending,
            after: after,
            limit: 25
        ))
        let rows = response.results.map {
            forwardRow(
                id: $0.id,
                icon: "shield.lefthalf.filled",
                title: rawText($0.flagType),
                detail: .joined([
                    appText($0.resolvedAt == nil ? .nativeSwiftRouteSurfacePending : .nativeSwiftRouteSurfaceResolved),
                    countText($0.reporterCount, item: "reporter")
                ])
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    private func loadAdminModlogPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let response: AdminModlogResponse = try await client.send(.adminModlog(after: after, limit: 25))
        let rows = response.results.compactMap { result -> NativeForwardRow? in
            guard let action = response.moderatorActions[result.id] else { return nil }
            let actorName = action.actorUserId.flatMap { response.users[$0]?.username } ?? action.actorUserId
            return forwardRow(
                id: result.id,
                icon: "clock.arrow.circlepath",
                title: rawText(action.actionType),
                detail: .joined([
                    actorName.map(rawText) ?? appText(.nativeSwiftCommunityRowsSystem),
                    (action.reason ?? action.communityId).map(rawText) ?? appText(.nativeSwiftRouteSurfaceGlobal)
                ])
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    private func loadAppealPage(client: APIClient, after: String?, mine: Bool) async throws -> NativeForwardPage {
        let response: ModerationAppealListResponse = try await client.send(
            .appeals(limit: 25, after: after, mine: mine)
        )
        let rows = response.appeals.map {
            forwardRow(
                id: $0.id,
                icon: "arrow.uturn.left.circle",
                title: rawText($0.caseId ?? $0.id),
                detail: appText($0.status.titleKey)
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    private func loadMyDisputePage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let response: ModerationDisputeListResponse = try await client.send(
            .disputes(limit: 25, after: after, mine: true)
        )
        let rows = response.disputes.map {
            forwardRow(
                id: $0.id,
                icon: "exclamationmark.triangle",
                title: rawText($0.postId ?? $0.id),
                detail: appText($0.status.titleKey)
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    private func loadReviewDisputePage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        let response: ReviewDisputeListResponse = try await client.send(.disputes(limit: 25, after: after))
        let rows = response.disputes.map {
            forwardRow(
                id: $0.id,
                icon: "exclamationmark.bubble",
                title: rawText($0.claimText),
                detail: .joined([
                    appText($0.status.titleKey),
                    $0.recommendedAction.map { appText($0.titleKey) }
                ].compactMap { $0 })
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }
}
