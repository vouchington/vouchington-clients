import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct StaffModerationReportCard: View {
    @Environment(\.locale)
    var nativeUiLocale
    let report: StaffModerationReport
    @Bindable
    var viewModel: ModerationReportsViewModel
    let onNavigate: (String) -> Void
    let onConfirm: (ModerationReportsConfirmation) -> Void
    @State
    private var warningPresented = false

    var body: some View {
        GroupBox {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                HStack(alignment: .top) {
                    Button {
                        viewModel.toggleSelection(report.id)
                    } label: {
                        Image(systemName: viewModel.selectedReportIds.contains(report.id)
                            ? "checkmark.square.fill" : "square")
                    }
                    .buttonStyle(.plain)
                    .disabled(viewModel.isQueueActionInProgress || viewModel.inFlightReportIds.contains(report.id))
                    reportHeader(
                        label: report.targetLabel ?? report.entityType,
                        status: report.status,
                        path: report.targetPath,
                        locale: nativeUiLocale,
                        onNavigate: onNavigate
                    )
                }
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsReportSummary,
                    parameters: [
                        "reports": reportCountText(report.reportCount),
                        "reason": report.reason,
                        "date": UiMessages.date(
                            report.createdAt,
                            date: .abbreviated,
                            time: .shortened,
                            locale: nativeUiLocale,
                            timeZone: .current
                        )
                    ],
                    locale: nativeUiLocale
                ))
                if let content = report.targetContent,
                   let text = NormalizedAuthoredText(
                       text: content.text,
                       declaredLanguage: content.declaredLanguage,
                       detectedLanguage: content.linguaRsDetectedLanguage
                   ) {
                    Text(text.value)
                        .font(Typography.subheadline)
                        .authoredContentLanguage(
                            declared: text.declaredLanguage,
                            detected: text.detectedLanguage
                        )
                }
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsReporter,
                    parameters: ["reporter": report.reporterUsername ?? report.reporterUserId],
                    locale: nativeUiLocale
                ))
                if let note = report.note {
                    Text(UiMessages.string(
                        .nativeSwiftModerationReportsNote,
                        parameters: ["note": note],
                        locale: nativeUiLocale
                    ))
                }
                if let context = report.postModerationContext {
                    Text(UiMessages.string(
                        .nativeSwiftModerationReportsModerationContext,
                        parameters: ["context": String(describing: context)],
                        locale: nativeUiLocale
                    ))
                }
                judgement
                banEvasion
                if let error = viewModel.actionErrors[report.id] {
                    Label(UiMessages.string(error, locale: nativeUiLocale), systemImage: "exclamationmark.triangle")
                        .foregroundStyle(Colors.negativeVote)
                }
                ModerationReportActionButtons(
                    report: report,
                    viewModel: viewModel,
                    onWarning: { warningPresented = true },
                    onConfirm: onConfirm
                )
            }
            .font(Typography.caption)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .sheet(isPresented: $warningPresented) {
            ModerationReportWarningDialog(
                reportId: report.id,
                viewModel: viewModel,
                isPresented: $warningPresented
            )
        }
    }

}

func reportHeader(
    label: String,
    status: ModerationReportStatus,
    path: String?,
    locale: Locale,
    onNavigate: @escaping (String) -> Void
) -> some View {
    HStack {
        if let path {
            Button(label) { onNavigate(path) }
                .buttonStyle(.plain)
                .foregroundStyle(.tint)
        } else {
            Text(label).font(Typography.headline)
        }
        Spacer()
        Text(UiMessages.string(status.titleKey, locale: locale)).foregroundStyle(.secondary)
    }
}
