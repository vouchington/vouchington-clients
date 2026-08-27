import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct StaffModerationReportCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
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

    @ViewBuilder
    private var judgement: some View {
        if let judgement = report.judgement {
            VStack(alignment: .leading) {
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsAiJudgement,
                    parameters: ["action": judgement.recommendedAction],
                    locale: nativeUiLocale
                ))
                Text(judgement.internalResponse)
                if !judgement.publicResponse.isEmpty {
                    Text(judgement.publicResponse)
                }
                if judgement.isStale {
                    Label(
                        UiMessages.string(.nativeSwiftModerationReportsOutdated, locale: nativeUiLocale),
                        systemImage: "clock.badge.exclamationmark"
                    )
                }
            }
        } else {
            Text(UiMessages.string(
                .nativeSwiftModerationReportsAiJudgementUnavailable,
                locale: nativeUiLocale
            )).foregroundStyle(.secondary)
        }
    }

    @ViewBuilder
    private var banEvasion: some View {
        if let context = report.communityBanEvasion {
            VStack(alignment: .leading) {
                Label(
                    UiMessages.string(.nativeSwiftModerationReportsBanEvasionSignal, locale: nativeUiLocale),
                    systemImage: "person.crop.circle.badge.exclamationmark"
                )
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsCommunity,
                    parameters: ["community": context.communitySlug],
                    locale: nativeUiLocale
                ))
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsMatchedAccount,
                    parameters: ["account": context.sourceUsername ?? context.sourceUserId],
                    locale: nativeUiLocale
                ))
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsScore,
                    parameters: ["score": UiMessages.percent(context.score, locale: nativeUiLocale)],
                    locale: nativeUiLocale
                ))
            }
        }
    }

    private func reportCountText(_ count: Int) -> String {
        UiMessages.string(
            UiMessage(
                .nativeSwiftModerationReportsReportCount,
                numberParameters: ["count": Double(count)]
            ),
            locale: nativeUiLocale
        )
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
