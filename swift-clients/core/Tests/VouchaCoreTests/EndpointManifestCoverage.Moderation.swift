import VouchaAPI
import VouchaModels

extension EndpointManifestCoverage {
    static let moderationEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.moderation.reports.default") {
            Endpoint.moderationReports(status: .pending, limit: 25)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.reports.member.default") {
            Endpoint.moderationReports(status: .pending, limit: 25)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.reports.clustered.default") {
            Endpoint.clusteredModerationReports(status: .pending, limit: 4)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.reports.clustered.page-2") {
            Endpoint.clusteredModerationReports(
                status: .pending,
                after: "eyJjbHVzdGVyIjp0cnVlLCJjcmVhdGVkX2F0IjoiMjAyNi0wNi0wMVQxMjowNTowMC4wMDAwMDBaIiwiZW50aXR5X3R5cGUiOiJwb3N0IiwiaWQiOiIwMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDAxMDMiLCJzb3J0IjoiY3JlYXRlZF9hdF9kZXNjIiwic3RhdHVzIjoicGVuZGluZyIsInNjb3BlIjoie1wiYXVkaWVuY2VcIjpcInN0YWZmXCIsXCJvd25lcklkXCI6bnVsbH0ifQ",
                limit: 3
            )
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-judgement.default") {
            Endpoint.rerunModerationReportJudgement(reportId: "019f6559-6b31-7171-b5b1-ccd9d702c45e")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-resolution.reviewed") {
            Endpoint.resolveModerationReport(
                reportId: "019f6559-6b31-7171-b5b1-ccd9d702c45e",
                status: .reviewed
            )
        },
        ManifestRegisteredEndpoint(id: "native.moderation.admin-warning.report") {
            fixtureAdminWarningEndpoint()
        },
        ManifestRegisteredEndpoint(id: "native.moderation.ban-evasion.confirm") {
            Endpoint.confirmCommunityBanEvasion(communityIdOrSlug: "community-1", userId: "user-2")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.ban-evasion.dismiss") {
            Endpoint.dismissCommunityBanEvasion(communityIdOrSlug: "community-1", userId: "user-2")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.default") {
            Endpoint.appeals(status: .pending, limit: 25)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.page-2") {
            Endpoint.appeals(status: .pending, limit: 25, after: pendingAppealsPageCursor)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.update.default") {
            Endpoint.updateAppeal(
                id: pendingAppealId,
                publicResponse: "We reviewed your appeal and reduced the action."
            )
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.approval.default") {
            Endpoint.appealApproval(id: pendingAppealId)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.delivery.default") {
            Endpoint.appealDelivery(id: pendingAppealId)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.resolution.accept") {
            Endpoint.appealResolution(id: pendingAppealId, action: .accept)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.resolution.reduce") {
            Endpoint.appealResolution(id: pendingAppealId, action: .reduce)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.resolution.deny") {
            Endpoint.appealResolution(id: pendingAppealId, action: .deny)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.appeals.resolution-drafts.default") {
            Endpoint.appealResolutionDrafts(id: pendingAppealId)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.disputes.default") {
            Endpoint.disputes(status: .pending, limit: 25)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.review-queue.default") {
            Endpoint.adminReviewQueue(limit: 2)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.review-queue.page-2") {
            Endpoint.adminReviewQueue(after: reviewQueuePageOneEndCursor, limit: 2)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.clearance.approved") {
            Endpoint.updatePostClearance(postId: reviewQueueInReviewPostId, status: .approved)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.clearance.rejected") {
            Endpoint.updatePostClearance(postId: reviewQueueInReviewPostId, status: .rejected)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.clearance.in-review") {
            Endpoint.updatePostClearance(postId: reviewQueueRejectedPostId, status: .inReview)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.modlog.default") {
            Endpoint.adminModlog()
        },
        ManifestRegisteredEndpoint(id: "native.moderation.analytics.default") {
            Endpoint.adminModerationAnalytics(range: "30d")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.transparency.default") {
            Endpoint.moderationTransparency(range: "30d")
        },
        ManifestRegisteredEndpoint(id: "native.community.moderation-transparency.default") {
            Endpoint.communityModerationTransparency(idOrSlug: "fixture-community", range: "30d")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.default") {
            Endpoint.voteIntegrityFlags()
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.pending") {
            Endpoint.voteIntegrityFlags(status: .pending)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.resolved") {
            Endpoint.voteIntegrityFlags(status: .resolved)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.resolution.dismissed") {
            Endpoint.resolveVoteIntegrityFlag(id: "vote-flag-1", resolution: .dismissed)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.resolution.penalized") {
            Endpoint.resolveVoteIntegrityFlag(id: "vote-flag-1", resolution: .penalized)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.resolution.suspended") {
            Endpoint.resolveVoteIntegrityFlag(id: "vote-flag-1", resolution: .suspended)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.penalty") {
            Endpoint.applyVoteIntegrityPenalty(flagId: "vote-flag-1")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.default") {
            Endpoint.reportIntegrityFlags()
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.pending") {
            Endpoint.reportIntegrityFlags(status: .pending)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.resolved") {
            Endpoint.reportIntegrityFlags(status: .resolved)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.resolution.dismissed") {
            Endpoint.dismissReportIntegrityFlag(id: "report-flag-1")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.penalty") {
            Endpoint.applyReportIntegrityPenalty(flagId: "report-flag-1")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.penalties.default") {
            Endpoint.reportIntegrityPenalties()
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.penalties.active") {
            Endpoint.reportIntegrityPenalties(status: .active)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.penalties.revoked") {
            Endpoint.reportIntegrityPenalties(status: .revoked)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.penalties.all") {
            Endpoint.reportIntegrityPenalties()
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.penalties.page-2") {
            Endpoint.reportIntegrityPenalties(status: .active, after: reportPenaltyPageCursor)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.penalties.get") {
            Endpoint.reportIntegrityPenalty(id: "019f7000-0000-7000-8000-000000000101")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.report-integrity.penalties.revoke") {
            Endpoint.revokeReportIntegrityPenalty(id: "019f7000-0000-7000-8000-000000000101")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.penalties.default") {
            Endpoint.voteIntegrityPenalties()
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.penalties.active") {
            Endpoint.voteIntegrityPenalties(status: .active)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.penalties.revoked") {
            Endpoint.voteIntegrityPenalties(status: .revoked)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.penalties.all") {
            Endpoint.voteIntegrityPenalties()
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.penalties.page-2") {
            Endpoint.voteIntegrityPenalties(status: .active, after: votePenaltyPageCursor)
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.penalties.get") {
            Endpoint.voteIntegrityPenalty(id: "019f7000-0000-7000-8000-000000000201")
        },
        ManifestRegisteredEndpoint(id: "native.moderation.vote-integrity.penalties.revoke") {
            Endpoint.revokeVoteIntegrityPenalty(id: "019f7000-0000-7000-8000-000000000201")
        }
    ]
}

private func fixtureAdminWarningEndpoint() -> Endpoint {
    do {
        return try Endpoint.issueAdminWarning(
            userId: "user-2",
            reason: "Repeated harassment",
            publicMessage: "Stop contacting this user.",
            reportId: "019f6559-6b31-7171-b5b1-ccd9d702c45e"
        )
    } catch {
        fatalError("Static fixture warning input must remain valid: \(error)")
    }
}
