import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct VoteIntegrityFlagCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let viewModel: VoteIntegrityViewModel
    let flag: VoteIntegrityFlag
    let onNavigate: (String) -> Void
    let onRequestPenalty: () -> Void

    var body: some View {
        let target = voteIntegrityTarget(flag)
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Label(flagTypeText, systemImage: "checkmark.shield")
                .font(Typography.headline)
            targetRow(target)
            LabeledContent(text(.nativeSwiftIntegrityFlagId), value: flag.id)
            LabeledContent(text(.nativeSwiftIntegrityCreated), value: date(flag.createdAt))
            lifecycleRows
            if !flag.details.isEmpty {
                Divider()
                Text(text(.nativeSwiftIntegrityEvidence)).font(Typography.subheadline.weight(.semibold))
                ForEach(integrityDetails(flag.details, locale: nativeUiLocale), id: \.self) {
                    Text(verbatim: UiMessages.string(.verbatim($0), locale: nativeUiLocale))
                        .font(Typography.caption.monospaced())
                }
            }
            if let count = viewModel.penalizedUserCounts[flag.id] {
                Text(text(.nativeSwiftIntegrityPenalizedUsers, parameters: [
                    "count": UiMessages.number(count, locale: nativeUiLocale)
                ]))
                .foregroundStyle(Colors.secondaryLabel)
            }
            if viewModel.mutationErrorMessages[flag.id] != nil {
                Text(text(viewModel.reconciliationRequiredFlagIds.contains(flag.id)
                        || viewModel.ambiguousPenaltyFlagIds.contains(flag.id)
                        ? .nativeSwiftIntegrityResultUncertain
                        : .nativeSwiftIntegrityActionFailed))
                    .foregroundStyle(.red)
                    .accessibilityIdentifier("vote-integrity-\(flag.id)-error")
                if viewModel.reconciliationRequiredFlagIds.contains(flag.id) {
                    Button(text(.nativeSwiftIntegrityReconcile)) {
                        Task { await viewModel.reconcile(flag.id) }
                    }
                    .disabled(viewModel.reconcilingFlagIds.contains(flag.id))
                }
            }
            if flag.resolvedAt == nil {
                actionRows
            }
        }
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .accessibilityIdentifier("vote-integrity-\(flag.id)")
    }

    @ViewBuilder
    private func targetRow(_ target: IntegrityTarget) -> some View {
        if let path = target.path {
            Button(targetText(target)) { onNavigate(path) }
                .accessibilityLabel(text(.nativeSwiftIntegrityOpenTarget, parameters: ["target": targetText(target)]))
        } else {
            LabeledContent(text(.nativeSwiftIntegrityTarget), value: targetText(target))
        }
    }

    @ViewBuilder
    private var lifecycleRows: some View {
        if let resolution = flag.resolution {
            LabeledContent(text(.nativeSwiftIntegrityResolution), value: resolutionText(resolution))
        } else {
            LabeledContent(text(.nativeSwiftIntegrityResolution), value: text(.nativeSwiftRouteSurfacePending))
        }
        if let resolvedAt = flag.resolvedAt {
            LabeledContent(text(.nativeSwiftIntegrityResolvedAt), value: date(resolvedAt))
        }
        if let resolvedById = flag.resolvedById {
            LabeledContent(text(.nativeSwiftIntegrityResolvedBy), value: resolvedById)
        }
    }

    private var actionRows: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack {
                resolutionButton(.nativeSwiftIntegrityDismiss, .dismissed)
                resolutionButton(.nativeSwiftIntegrityMarkPenalized, .penalized)
                resolutionButton(.nativeSwiftIntegritySuspend, .suspended)
            }
            Button(text(.nativeSwiftIntegrityApplyVotePenalty), role: .destructive) {
                onRequestPenalty()
            }
            .disabled(!viewModel.canApplyPenalty(flag))
        }
    }

    private func resolutionButton(
        _ title: UiMessageKey,
        _ resolution: VoteIntegrityResolution
    ) -> some View {
        Button(text(title)) {
            Task { await viewModel.resolve(flag, as: resolution) }
        }
        .disabled(!viewModel.canResolve(flag))
    }

    private var flagTypeText: String {
        switch flag.flagType {
        case "velocity_spike": text(.nativeSwiftIntegrityVelocitySpike)
        case "ip_correlation": text(.nativeSwiftIntegrityIpCorrelation)
        default: UiMessages.string(.verbatim(flag.flagType), locale: nativeUiLocale)
        }
    }

    private func resolutionText(_ resolution: VoteIntegrityResolution) -> String {
        switch resolution {
        case .dismissed: text(.nativeSwiftIntegrityDismiss)
        case .penalized: text(.nativeSwiftIntegrityMarkPenalized)
        case .suspended: text(.nativeSwiftIntegritySuspend)
        }
    }

    private func targetText(_ target: IntegrityTarget) -> String {
        text(target.kind.messageKey, parameters: ["id": target.id])
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
