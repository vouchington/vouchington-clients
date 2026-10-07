import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadStaffModerationRows(
        client: APIClient,
        path: String
    ) async throws -> [NativeRouteDestinationRow]? {
        switch path {
        case _ where path.contains("disputes"):
            try await loadReviewDisputeRows(client: client)
        case "/reports":
            try await loadReportRows(client: client)
        case "/admin/modlog":
            try await loadAdminModlogRows(client: client)
        case "/admin/moderation-analytics":
            try await loadAdminAnalyticsRows(client: client)
        default:
            nil
        }
    }

    private func loadReviewDisputeRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: ReviewDisputeListResponse = try await client.send(.disputes(limit: 25))
        return response.disputes.map {
            row(
                "exclamationmark.bubble",
                rawText($0.claimText),
                .joined(
                    [
                        appText($0.status.titleKey),
                        $0.recommendedAction.map { appText($0.titleKey) }
                    ].compactMap { $0 }
                )
            )
        }
    }

    private func loadReportRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: StaffClusteredModerationReportsResponse = try await client.send(.clusteredModerationReports(
            status: .pending,
            limit: 25
        ))
        return response.duplicateClusters.map {
            row(
                "flag",
                rawText($0.signal),
                .joined([
                    countText($0.postCount, item: "post"),
                    countText($0.reportCount, item: "report")
                ])
            )
        } + response.clusters.map {
            row(
                "flag",
                rawText($0.targetLabel ?? $0.entityType),
                .joined([
                    countText($0.reportCount, item: "report"),
                    countText($0.reporterCount, item: "reporter")
                ])
            )
        }
    }

    private func loadAdminModlogRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: AdminModlogResponse = try await client.send(.adminModlog(limit: 25))
        return response.results.compactMap { result in
            guard let action = response.moderatorActions[result.id] else { return nil }
            let actorName = action.actorUserId.flatMap { response.users[$0]?.username } ?? action.actorUserId
            return row(
                "clock.arrow.circlepath",
                rawText(action.actionType),
                .joined([
                    actorName.map(rawText) ?? appText(.nativeSwiftCommunityRowsSystem),
                    (action.reason ?? action.communityId).map(rawText)
                        ?? appText(.nativeSwiftRouteSurfaceGlobal)
                ])
            )
        }
    }

    private func loadAdminAnalyticsRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: ModerationAnalytics = try await client.send(.adminModerationAnalytics(range: "30d"))
        return [
            row(
                "chart.bar.xaxis",
                appText(.nativeSwiftCommunityRowsQueueVolume),
                .joined([
                    appText(
                        .nativeSwiftRouteSurfacePendingCount,
                        numberParameters: ["count": Double(response.queueVolume.pendingReports)]
                    ),
                    appText(
                        .nativeSwiftRouteSurfaceTotalCount,
                        numberParameters: ["count": Double(response.queueVolume.totalReports)]
                    )
                ])
            ),
            row(
                "arrow.uturn.left.circle",
                appText(.nativeSwiftCommunityRowsAppeals),
                .joined([
                    appText(
                        .nativeSwiftRouteSurfaceClosedCount,
                        numberParameters: ["count": Double(response.appeals.totalClosed)]
                    ),
                    response.appeals.successRate.map {
                        appText(.nativeSwiftRouteSurfaceSuccessRate, percentParameters: ["rate": $0])
                    } ?? appText(.nativeSwiftRouteSurfaceNotAvailable)
                ])
            ),
            row(
                "person.3",
                appText(.nativeSwiftRouteSurfaceModeratorWorkload),
                countText(response.moderatorWorkload.moderators.count, item: "moderator")
            )
        ]
    }
}
