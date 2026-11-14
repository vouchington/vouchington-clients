import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct IntegrityPenaltyCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let viewModel: IntegrityPenaltyLedgerViewModel
    let penalty: IntegrityPenaltyRow
    let onNavigate: (String) -> Void
    let onRequestRevoke: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            LabeledContent(text(.nativeSwiftIntegrityReason), value: reasonText)
                .font(Typography.headline)
            LabeledContent(text(.nativeSwiftIntegrityUser)) {
                Button(userText) { onNavigate("/user/\(penalty.userId)") }
                    .accessibilityLabel(openTarget(userText))
            }
            if penalty.sourceFlagId != nil {
                LabeledContent(
                    text(.nativeSwiftIntegritySourceFlag),
                    value: penalty.sourceFlagType ?? text(.nativeSwiftRouteSurfaceNotAvailable)
                )
            }
            LabeledContent(
                text(.nativeSwiftIntegrityCreatedBy),
                value: moderationIdentityLabel(
                    displayName: penalty.creatorDisplayName,
                    username: penalty.creatorUsername,
                    locale: nativeUiLocale
                )
            )
            LabeledContent(text(.nativeSwiftIntegrityCreated), value: date(penalty.createdAt))
            if let multiplier = penalty.multiplier {
                LabeledContent(
                    text(.nativeSwiftIntegrityMultiplier),
                    value: UiMessages.percent(multiplier, locale: nativeUiLocale)
                )
            }
            if let revokedAt = penalty.revokedAt {
                LabeledContent(text(.nativeSwiftIntegrityRevokedAt), value: date(revokedAt))
            }
            if penalty.revokedAt != nil {
                LabeledContent(
                    text(.nativeSwiftIntegrityRevokedBy),
                    value: moderationIdentityLabel(
                        displayName: penalty.revokerDisplayName,
                        username: penalty.revokerUsername,
                        locale: nativeUiLocale
                    )
                )
            }
            if let error = viewModel.mutationErrors[penalty.id] {
                Text(text(error.messageKey)).foregroundStyle(.red)
                    .accessibilityIdentifier("integrity-penalty-\(penalty.id)-error")
                if viewModel.reconciliationRequiredIds.contains(penalty.id) {
                    Button(text(.nativeSwiftIntegrityReconcile)) {
                        Task { await viewModel.reconcile(penalty.id) }
                    }
                    .disabled(viewModel.reconcilingPenaltyIds.contains(penalty.id))
                }
            }
            if viewModel.canRevoke(penalty) {
                Button(text(.nativeSwiftIntegrityRevoke), role: .destructive, action: onRequestRevoke)
            }
        }
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .accessibilityIdentifier(penalty.id)
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

    private func openTarget(_ target: String) -> String {
        text(.nativeSwiftIntegrityOpenTarget, parameters: ["target": target])
    }

    private func date(_ value: Date) -> String {
        UiMessages.abbreviatedDateTime(
            value,
            locale: nativeUiLocale
        )
    }

    private func text(_ key: UiMessageKey, parameters: [String: String] = [:]) -> String {
        UiMessages.string(key, parameters: parameters, locale: nativeUiLocale)
    }
}
