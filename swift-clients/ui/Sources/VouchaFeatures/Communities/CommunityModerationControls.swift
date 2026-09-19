import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityModerationAdminControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void

    @State
    private var postId = ""
    @State
    private var reportId = ""
    @State
    private var moderationReason = ""
    @State
    private var warningUserId = ""
    @State
    private var warningPublicMessage = ""
    @State
    private var banUserId = ""
    @State
    private var banReason = ""
    @State
    private var restrictionId = ""
    @State
    private var restrictionTypes = "require_post_approval"
    @State
    private var restrictionReason = ""
}

extension CommunityModerationAdminControls {
    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftCommunitiesModeration, locale: nativeUiLocale)).font(Typography.headline)

            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeSwiftCommunitiesPostReview, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                TextField(UiMessages.string(.nativeSwiftCommunitiesPostId, locale: nativeUiLocale), text: $postId)
                    .textFieldStyle(.roundedBorder)
                moderationButton(.nativeModerationSummaryTitle, systemImage: "checkmark.shield") {
                    Task { await viewModel.loadModerationResults(postId: postId) }
                }
                ForEach(viewModel.moderationResults) { row in
                    NativeSurfaceRow(row: row)
                }
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesRejectReason, locale: nativeUiLocale),
                    text: $moderationReason
                ).textFieldStyle(.roundedBorder)
                HStack {
                    moderationButton(.nativeSwiftCommonApprove, systemImage: "checkmark.circle") {
                        Task { await viewModel.approvePendingPost(postId: postId) }
                    }
                    moderationButton(.nativeSwiftCommunitiesReject, systemImage: "xmark.circle") {
                        Task { await viewModel.rejectPendingPost(postId: postId, reason: moderationReason) }
                    }
                    moderationButton(.nativeSwiftCommunityActionsUnpublish, systemImage: "eye.slash") {
                        Task { await viewModel.unpublishPendingPost(postId: postId) }
                    }
                    moderationButton(.nativeSwiftCommunityActionsClaim, systemImage: "hand.raised") {
                        Task { await viewModel.claimPendingPost(postId: postId) }
                    }
                    moderationButton(.nativeSwiftCommunityActionsRelease, systemImage: "hand.thumbsdown") {
                        Task { await viewModel.releasePendingPost(postId: postId) }
                    }
                    moderationButton(.nativeSwiftCommunityActionsEscalate, systemImage: "arrow.up.circle") {
                        Task { await viewModel.escalatePendingPost(postId: postId) }
                    }
                }
            }

            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeSwiftCommunitiesReportReview, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                TextField(UiMessages.string(.nativeSwiftCommunitiesReportId, locale: nativeUiLocale), text: $reportId)
                    .textFieldStyle(.roundedBorder)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesUserId, locale: nativeUiLocale),
                    text: $warningUserId
                ).textFieldStyle(.roundedBorder)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesWarningReason, locale: nativeUiLocale),
                    text: $moderationReason
                ).textFieldStyle(.roundedBorder)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesPublicMessage, locale: nativeUiLocale),
                    text: $warningPublicMessage
                ).textFieldStyle(.roundedBorder)
                HStack {
                    moderationButton(.nativeSwiftCommunityActionsWarn, systemImage: "exclamationmark.triangle") {
                        Task {
                            await viewModel.issueCommunityWarning(
                                userId: warningUserId,
                                reason: moderationReason,
                                publicMessage: warningPublicMessage,
                                reportId: reportId,
                                resolveReport: true
                            )
                        }
                    }
                    moderationButton(.nativeSwiftCommunityActionsClaim, systemImage: "hand.raised") {
                        Task { await viewModel.claimModerationReport(reportId: reportId) }
                    }
                    moderationButton(.nativeSwiftCommunityActionsRelease, systemImage: "hand.thumbsdown") {
                        Task { await viewModel.releaseModerationReport(reportId: reportId) }
                    }
                    moderationButton(.nativeSwiftCommunityActionsEscalate, systemImage: "arrow.up.circle") {
                        Task { await viewModel.escalateModerationReport(reportId: reportId) }
                    }
                }
            }

            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeSwiftCommunitiesBans, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                TextField(UiMessages.string(.nativeSwiftCommunitiesUserId, locale: nativeUiLocale), text: $banUserId)
                    .textFieldStyle(.roundedBorder)
                TextField(UiMessages.string(.nativeSwiftCommunitiesReason, locale: nativeUiLocale), text: $banReason)
                    .textFieldStyle(.roundedBorder)
                HStack {
                    moderationButton(.nativeSwiftCommunityActionsBan, systemImage: "nosign") {
                        Task { await viewModel.banMember(userId: banUserId, reason: banReason, expiresAt: nil) }
                    }
                    moderationButton(.nativeSwiftCommunityActionsLiftBan, systemImage: "arrow.uturn.left") {
                        Task { await viewModel.liftBan(userId: banUserId) }
                    }
                }
            }

            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(.nativeSwiftCommunitiesRestrictions, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesRestrictionTypes, locale: nativeUiLocale),
                    text: $restrictionTypes
                ).textFieldStyle(.roundedBorder)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesRestrictionId, locale: nativeUiLocale),
                    text: $restrictionId
                ).textFieldStyle(.roundedBorder)
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesReason, locale: nativeUiLocale),
                    text: $restrictionReason
                ).textFieldStyle(.roundedBorder)
                HStack {
                    moderationButton(.nativeSwiftCommunityActionsActivate, systemImage: "lock") {
                        Task {
                            await viewModel.activateRestrictions(
                                restrictionTypes: parsedRestrictionTypes(),
                                reason: restrictionReason
                            )
                        }
                    }
                    moderationButton(.nativeSwiftCommunityActionsLift, systemImage: "lock.open") {
                        Task { await viewModel.liftRestriction(restrictionId: restrictionId) }
                    }
                }
            }
        }
    }

    private func moderationButton(
        _ title: UiMessageKey,
        systemImage: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(UiMessages.string(title, locale: nativeUiLocale), systemImage: systemImage) {
            guard isSignedIn else {
                showSignIn()
                return
            }
            action()
        }
    }

    private func parsedRestrictionTypes() -> [CommunityRestrictionType] {
        restrictionTypes
            .split(separator: ",")
            .compactMap { token in
                CommunityRestrictionType(rawValue: token.trimmingCharacters(in: .whitespacesAndNewlines))
            }
    }
}
