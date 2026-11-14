import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct IntegrityPenaltyReviewCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let viewModel: IntegrityPenaltyReviewViewModel
    let penalty: IntegrityPenaltyRow
    let onNavigate: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            LabeledContent(text(.nativeSwiftIntegrityReason), value: reasonText)
            LabeledContent(text(.nativeSwiftIntegrityUser)) {
                Button(userText) { onNavigate("/user/\(penalty.userId)") }
            }
            if let due = penalty.reviewDueAt {
                LabeledContent(
                    text(.moderationPenaltyReviewsDue),
                    value: UiMessages.abbreviatedDateTime(due, locale: nativeUiLocale)
                )
            }
            if let error = viewModel.errors[penalty.id] {
                Text(text(error.messageKey)).foregroundStyle(.red)
                if viewModel.reconciliationRequiredIds.contains(penalty.id) {
                    Button(text(.nativeSwiftIntegrityReconcile)) {
                        Task { await viewModel.reconcile(penalty.id) }
                    }
                    .disabled(viewModel.mutatingIds.contains(penalty.id))
                }
            }
            HStack {
                if viewModel.canConfirm(penalty) {
                    Button(text(.moderationPenaltyReviewsConfirm)) {
                        Task { await viewModel.confirmReview(penalty) }
                    }
                }
                if viewModel.canRevoke(penalty) {
                    Button(text(.nativeSwiftIntegrityRevoke), role: .destructive) {
                        Task { await viewModel.revoke(penalty) }
                    }
                }
            }
        }
        .padding(Spacing.sm)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .accessibilityIdentifier("review-\(penalty.id)")
    }

    private var reasonText: String {
        switch penalty.reason {
        case "mass_report_campaign": text(.nativeSwiftIntegrityMassReportCampaign)
        case "voting_ring": text(.nativeSwiftIntegrityVotingRing)
        default: UiMessages.string(.verbatim(penalty.reason), locale: nativeUiLocale)
        }
    }

    private var userText: String {
        moderationIdentityLabel(
            displayName: penalty.userDisplayName,
            username: penalty.userUsername,
            locale: nativeUiLocale
        )
    }

    private func text(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: nativeUiLocale)
    }
}

extension IntegrityReviewError {
    var messageKey: UiMessageKey {
        switch self {
        case .loadFailed: .moderationPenaltyReviewsLoadFailed
        case .loadMoreFailed: .nativeSwiftIntegrityLoadMoreFailed
        case .permissionDenied: .nativeSwiftIntegrityAdministratorRequired
        case .reviewFailed: .nativeSwiftIntegrityActionFailed
        case .resultUncertain: .nativeSwiftIntegrityResultUncertain
        case .reconciliationFailed: .nativeSwiftIntegrityReconciliationFailed
        case .revokeFailed: .nativeSwiftIntegrityRevokeFailed
        }
    }
}
