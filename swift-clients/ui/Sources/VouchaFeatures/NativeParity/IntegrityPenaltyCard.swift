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
            LabeledContent(text(.nativeSwiftIntegrityPenaltyId), value: penalty.id)
            if let sourceFlagId = penalty.sourceFlagId {
                LabeledContent(text(.nativeSwiftIntegritySourceFlag), value: sourceFlagId)
            }
            if let creator = penalty.createdById {
                LabeledContent(text(.nativeSwiftIntegrityCreatedBy), value: creator)
            }
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
            if let revokedBy = penalty.revokedById {
                LabeledContent(text(.nativeSwiftIntegrityRevokedBy), value: revokedBy)
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
        text(.nativeSwiftIntegrityUserId, parameters: ["id": penalty.userId])
    }

    private func openTarget(_ target: String) -> String {
        text(.nativeSwiftIntegrityOpenTarget, parameters: ["target": target])
    }

    private func date(_ value: Date) -> String {
        UiMessages.date(
            value,
            date: .abbreviated,
            time: .shortened,
            locale: nativeUiLocale,
            timeZone: .current
        )
    }

    private func text(_ key: UiMessageKey, parameters: [String: String] = [:]) -> String {
        UiMessages.string(key, parameters: parameters, locale: nativeUiLocale)
    }
}
