import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadModerationAnalyticsRows(
        client: APIClient,
        revision: Int,
        tab: CommunitySurfaceTab
    ) async throws -> [NativeRouteDestinationRow] {
        let context = CommunityTransparencyContext(
            revision: revision,
            tab: tab,
            range: moderationTransparencyRange
        )
        let analytics: CommunityModerationAnalytics = try await client.send(
            .communityModerationAnalytics(idOrSlug: slug, range: context.range.rawValue)
        )
        guard ownsCommunityModerationTransparencyRequest(context) else { return [] }
        let rawRows = moderationAnalyticsRows(analytics)
        guard ownsCommunityModerationTransparencyRequest(context) else { return [] }
        moderationAnalyticsRows = rawRows
        do {
            let transparencyRows = try await loadInitialModerationTransparencyRows(
                client: client,
                context: context
            )
            guard ownsCommunityModerationTransparencyRequest(context) else { return [] }
            return moderationAnalyticsRows + transparencyRows
        } catch {
            guard ownsCommunityModerationTransparencyRequest(context) else { return [] }
            try Task.checkCancellation()
            return moderationAnalyticsRows
        }
    }

    private func moderationAnalyticsRows(
        _ analytics: CommunityModerationAnalytics
    ) -> [NativeRouteDestinationRow] {
        [
            .init(
                icon: "chart.bar",
                title: .message(.nativeSwiftCommunityRowsQueueVolume),
                detail: queueVolumeDetail(analytics.queueVolume)
            ),
            .init(
                icon: "person.2",
                title: .message(.nativeSwiftCommunityRowsAppeals),
                detail: .message(
                    .nativeSwiftCommunityRowsAppealsSummary,
                    numberParameters: [
                        "closed": Double(analytics.appeals.totalClosed),
                        "accepted": Double(analytics.appeals.accepted)
                    ]
                )
            ),
            .init(
                icon: "sparkles",
                title: .message(.nativeSwiftCommunityRowsAutomod),
                detail: automodDetail(analytics.automodPerformance)
            ),
            .init(
                icon: "person.3",
                title: .message(.nativeSwiftCommunityRowsWorkload),
                detail: .count(analytics.moderatorWorkload.moderators.count, item: "moderator")
            ),
            .init(
                icon: "figure.run",
                title: .message(.nativeSwiftCommunityRowsNewUserFriction),
                detail: newUserFrictionDetail(analytics.newUserFriction)
            )
        ]
    }

    private func queueVolumeDetail(_ volume: CommunityModerationAnalyticsQueueVolume) -> UiVerbatimText {
        .message(
            .nativeSwiftCommunityRowsQueueVolumeSummary,
            numberParameters: [
                "reports": Double(volume.totalReports),
                "pending": Double(volume.pendingReports)
            ]
        )
    }

    private func automodDetail(_ performance: CommunityModAutomodPerformance) -> UiVerbatimText {
        .message(
            .nativeSwiftCommunityRowsAutomodSummary,
            numberParameters: [
                "actions": Double(performance.totalActions),
                "falsePositives": Double(performance.falsePositiveCount)
            ]
        )
    }

    private func newUserFrictionDetail(_ friction: CommunityModNewUserFriction) -> UiVerbatimText {
        .message(
            .nativeSwiftCommunityRowsNewUserFrictionSummary,
            numberParameters: [
                "firstPosts": Double(friction.firstPosts),
                "rejected": Double(friction.rejectedFirstPosts)
            ]
        )
    }
}
