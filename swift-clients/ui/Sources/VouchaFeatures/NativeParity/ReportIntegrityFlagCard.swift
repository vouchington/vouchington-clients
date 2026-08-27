import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ReportIntegrityFlagCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let viewModel: ReportIntegrityViewModel
    let flag: ReportIntegrityFlag
    let onNavigate: (String) -> Void
    let onRequestPenalty: () -> Void

    var body: some View {
        let target = reportIntegrityTarget(flag)
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Label(flagTypeText, systemImage: "flag.2.crossed")
                .font(Typography.headline)
            targetRow(target)
            LabeledContent(text(.nativeSwiftIntegrityFlagId), value: flag.id)
            LabeledContent(
                text(.nativeSwiftIntegrityReporterCount),
                value: UiMessages.number(flag.reporterCount, locale: nativeUiLocale)
            )
            LabeledContent(
                text(.nativeSwiftIntegrityNewAccountReporters),
                value: UiMessages.percent(flag.newAccountReporterPct, maximumFractionDigits: 1, locale: nativeUiLocale)
            )
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
            if let count = viewModel.penalizedReporterCounts[flag.id] {
                Text(text(.nativeSwiftIntegrityPenalizedReporters, parameters: [
                    "count": UiMessages.number(count, locale: nativeUiLocale)
                ]))
                .foregroundStyle(Colors.secondaryLabel)
            }
            if viewModel.mutationErrorMessages[flag.id] != nil {
                Text(text(viewModel.reconciliationRequiredFlagIds.contains(flag.id)
                        ? .nativeSwiftIntegrityResultUncertain
                        : .nativeSwiftIntegrityActionFailed))
                    .foregroundStyle(.red)
                    .accessibilityIdentifier("report-integrity-\(flag.id)-error")
                if viewModel.reconciliationRequiredFlagIds.contains(flag.id) {
                    Button(text(.nativeSwiftIntegrityReconcile)) {
                        Task { await viewModel.reconcile(flag.id) }
                    }
                    .disabled(viewModel.reconcilingFlagIds.contains(flag.id))
                }
            }
            if flag.resolvedAt == nil {
                actionRow
            }
        }
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .accessibilityIdentifier("report-integrity-\(flag.id)")
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

    private var actionRow: some View {
        HStack {
            Button(text(.nativeSwiftIntegrityDismiss)) {
                Task { await viewModel.dismiss(flag) }
            }
            .disabled(!viewModel.canResolve(flag))
            Button(text(.nativeSwiftIntegrityPenalizeReportersAction), role: .destructive) {
                onRequestPenalty()
            }
            .disabled(!viewModel.canApplyPenalty(flag))
        }
    }

    private var flagTypeText: String {
        flag.flagType == "mass_report_suspected"
            ? text(.nativeSwiftIntegrityMassReportSuspected)
            : UiMessages.string(.verbatim(flag.flagType), locale: nativeUiLocale)
    }

    private func resolutionText(_ resolution: ReportIntegrityResolution) -> String {
        switch resolution {
        case .dismissed: text(.nativeSwiftIntegrityDismiss)
        case .penalized: text(.nativeSwiftIntegrityMarkPenalized)
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
